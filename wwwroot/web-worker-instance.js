'use strict';

/*********************
 *WEB WORKER INSTANCE*
 *********************/

//////////////////
//INITIALIZATION//
//////////////////

var _token = null;
var _files = null;
var _settings = null;

///the main thread will send a message when the worker should bootstrap Blazor
addEventListener('message', function (e)
{
    //only listen for the first message, which will be the serialized settings and an auth token (if available)
    if (this._token == null && this._settings == null && e?.data?.settings != null)
    {
        //initialization
        importScripts('/_content/LinesOfCode.Web.Workers/web-worker-environment.js');
        import('/_content/LinesOfCode.Web.Workers/web-worker-common.js').then((common) =>
        {
            //set the properties that will be read by Program.cs
            this._token = e.data.token;
            this._settings = JSON.parse(common.arrayBufferToString(e.data.settings));

            //create window
            globalThis.window = createProxy(self, new Node());
            globalThis.window.parent = globalThis.window;

            //create document (Blazor also requires history to be instantiated)
            globalThis.document = createProxy(new DOM());
            globalThis.history = createProxy(new Comment());

            //set default dom base URL
            var url = globalThis.location.href;
            document.baseURI = url.substring(0, url.lastIndexOf('/') + 1);

            //fix the dom's base URL to ignore the '_content/[name of referenced project]' path as the Blazor script needs to be at the logical root of the app
            if (document.baseURI.indexOf('_content/') > 0)
                document.baseURI = new URL(document.baseURI + '../../').toString();

            //build a minimal DOM to host Blazor inside the web worker
            var body = document.appendChild(document.createElement('body'));
            var script = body.appendChild(document.createElement('script'));
            script.setAttribute('src', '_framework/blazor.webassembly.js');
            script.setAttribute('autostart', 'false');

            //load blazor
            document.loadBlazor();

            //return
            Blazor.start(
            {
                //designate that this is a web worker environment
                environment: 'WebWorker'
            });
        });
    }
});

/////////////////////////////
//PROGRAM.CS HELPER METHODS//
/////////////////////////////

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

////////////////////////////////
//FILE UPLOADER HELPER METHODS//
////////////////////////////////

///Called by a Blazor component to get a chunk of an uploaded file.
async function getFileChunkAsync(segmentNumber, segmentSize, fileIndex)
{
    //initialization
    var file = this._files[fileIndex];
    var segmentIndex = getSegmentIndex(segmentNumber, segmentSize);

    //check file
    if (!file)
    {
        //error
        console.warn(`File at index ${fileIndex} was not found.`);
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

            //process file
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