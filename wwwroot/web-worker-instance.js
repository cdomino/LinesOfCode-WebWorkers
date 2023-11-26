'use strict';

//initialization
var _token = null;
var _files = null;
var _settings = null;
this.postMessage = this.webkitPostMessage || this.postMessage;

//set other metadata from the main thread
this.addEventListener('message', function (e)
{
    //only listen for the first message, which will be the serialized settings
    if (this._token == null && this._settings == null && e.data != null && e.data.settings != null)
    {
        //return
        this._token = e.data.token;
        this._settings = JSON.parse(arrayBufferToString(e.data.settings));
    }
});

//bootstraps the minimal html/javascript infrastructure required to host Blazor
var initializeWebWorker = async () =>
{  
    //initialization
    importScripts('/_content/LinesOfCode.Web.Workers/web-worker-environment.js');

    //fix the dom's base URL to ignore the '_content/[name of referenced project]' path as the Blazor script needs to be at the logical root of the app
    if (document.baseURI.indexOf('_content/') > 0)
        document.baseURI = new URL(document.baseURI + '../../').toString();

    //build a minimal DOM to host Blazor inside the web worker
    var body = document.appendChild(document.createElement('body'));
    var script = body.appendChild(document.createElement('script'));
    script.setAttribute('src', '_framework/blazor.webassembly.js');
    script.setAttribute('autostart', 'false');

    //start Blazor
    document.loadBlazor();
    Blazor.start(
    {
        //designate that this is a web worker environment
        environment: 'WebWorker',
    }).then(() =>
    {
        //hook web worker message event
        globalThis.postMessage = globalThis.webkitPostMessage || globalThis.postMessage;
        globalThis.addEventListener('message', function (e)
        {
            //initialization
            if (e.data === 'start')
            {
                //already started
                globalThis.postMessage('started');
            }
            else
            {
                //get message
                var hasFiles = Array.isArray(e.data);
                var json = hasFiles ? arrayBufferToString(e.data[0]) : arrayBufferToString(e.data);

                //parse message
                var message = JSON.parse(json);
                console.log('Processing web worker command ' + message.command + '.');

                //check command
                switch (message.command)
                {
                    //set auth token
                    case 'token':

                        //call blazor method
                        globalThis.blazorReference.invokeMethodAsync(globalThis.tokenMethod, message.token)
                            .then(() =>
                            {
                                //return
                                globalThis.postMessage('token');
                            }, (error) =>
                            {
                                //initialization
                                var errorMessage =
                                {
                                    //assemble object
                                    command: 'error',
                                    error: error.message,
                                    invocationId: message.invocationId
                                };

                                //return
                                var json = JSON.stringify(errorMessage);
                                var transferrable = stringToArrayBuffer(json);
                                globalThis.postMessage(transferrable, [transferrable.buffer]);
                            });
                        break;

                    //call a method
                    case 'method':

                        //check files
                        if (hasFiles)
                        {
                            //get files
                            this._files = [];
                            for (var f = 1; f < e.data.length; f++)
                                this._files.push(e.data[f]);
                        }

                        //call blazor method
                        globalThis.blazorReference.invokeMethodAsync(globalThis.callbackMethod, message.invocationId, message.proxy, message.eventRegistrations)
                            .then((result) =>
                            {
                                //send result to UI thread
                                var resultMessage =
                                {
                                    //assemble object                                                             
                                    result: result,
                                    command: 'result',
                                    proxy: message.proxy,
                                    invocationId: message.invocationId
                                };

                                //return
                                var json = JSON.stringify(resultMessage);
                                var transferrable = stringToArrayBuffer(json);
                                globalThis.postMessage(transferrable, [transferrable.buffer]);
                            }, (error) =>
                            {
                                //initialization
                                var errorMessage =
                                {
                                    //assemble object
                                    command: 'error',
                                    error: error.message,
                                    proxy: message.proxy,
                                    invocationId: message.invocationId
                                };

                                //return
                                var json = JSON.stringify(errorMessage);
                                var transferrable = stringToArrayBuffer(json);
                                globalThis.postMessage(transferrable, [transferrable.buffer]);
                            });
                        break;

                    //error
                    default:
                        console.error('Unknown web worker command ' + message.command + '.');
                        break;
                }

            }
        }, false);

        //tell the UI thread that this worker has loaded Blazor
        globalThis.postMessage('started');
    });
};

//start blazor load
if (typeof globalThis.document === 'undefined')
    initializeWebWorker();

///////////////////
//PRIVATE METHODS//
///////////////////

///This sets the blazor callback for this worker and tells the UI thread that it is ready.
function connectWebWorker(blazorReference, callbackMethod, tokenMethod)
{
    //initialization
    globalThis.tokenMethod = tokenMethod;
    globalThis.callbackMethod = callbackMethod;
    globalThis.blazorReference = blazorReference;

    //return
    globalThis.postMessage('loaded');
}

///This sends a message to the UI thread in response to a web worker event being raised.
function marshalEvent(invocationId, proxy, name, type, value)
{
    //initialization
    var message =
    {
        //assemble object
        proxy: proxy,
        result: value,
        eventName: name,
        command: 'event',
        invocationId: invocationId,
        eventArgumentTypeName: type
    };

    //return
    var json = JSON.stringify(message);
    var transferrable = stringToArrayBuffer(json);
    globalThis.postMessage(transferrable, [transferrable.buffer]);
}

///This converts a UTF-8 array buffer to a string.
function arrayBufferToString(buffer)
{
    //initialization
    var decoder = new TextDecoder();
    var array = new Uint8Array(buffer);

    //return
    return decoder.decode(array);
}

///Encodes a string as a UTF-8 buffer.
function stringToArrayBuffer(value)
{
    //initialization
    var encoder = new TextEncoder();

    //return
    return encoder.encode(value);
}

///Returns the settings sent from the main UI thread.
function getWebWorkerSettings()
{
    //return
    return this._settings;
}

///Returns the token sent from the main UI thread.
function getWebWorkerToken()
{
    //return
    return this._token;
}

///Called by the server to get a chunk of an uploaded file.
async function getFileChunkAsync(segmentNumber, segmentSize, fileIndex)
{
    //initialization
    var file = this._files[fileIndex];
    var segmentIndex = getSegmentIndex(segmentNumber, segmentSize);

    //check file
    if (!file)
    {
        //error
        console.error('File at index ' + fileIndex + ' was not found.');
        return null;
    }

    //slice the proper chunk out of the file
    var segmentLength = getSegmentLength(file.size, segmentIndex, segmentSize);
    var chunk = getSegmentChunk(file, segmentIndex, segmentLength);

    //return
    return await readFileChunkAsync(chunk);
}

///Acquires a segment of a file directly from the raw data.
async function readFileChunkAsync(chunk)
{
    //initialization
    var reader = new FileReader();

    //read file
    var result = new Promise((resolve, reject) =>
    {
        //hook events
        reader.addEventListener('error', reject);
        reader.addEventListener('load', () =>
        {
            //parse chunk
            var base64Chunk = reader.result;
            var cleanChunk = base64Chunk.substr(base64Chunk.indexOf(',') + 1);
            resolve(cleanChunk);
        }, false);
    });

    //return
    reader.readAsDataURL(chunk);
    return result;
}

///Gets the index of a segment of a byte array
function getSegmentIndex(segmentNumber, segmentSize)
{
    //return
    return segmentNumber * segmentSize;
}

///Gets the length of a segment of a byte array
function getSegmentLength(size, segmentIndex, segmentSize)
{
    //initialization
    var length = size - segmentIndex;

    //return
    return length <= segmentSize ? length : segmentSize;
}

///Gets the content for a segment of a byte array
function getSegmentChunk(buffer, segmentIndex, segmentLength)
{
    //return
    return buffer.slice(segmentIndex, segmentIndex + segmentLength);
}

///Gets the number of files sent to this worker.
function getFileCount()
{
    //return
    if (this._files)
        return this._files.length;
    else
        return 0;
}

///Gets the size of a file by index.
function getFileSize(fileIndex)
{
    //initialization
    if (!this._files || fileIndex >= this._files.length)
        return 0;

    //return
    var file = this._files[fileIndex];
    if (file)
        return file.size;
    else
        return 0;
}