/****************
 *WORKER MANAGER*
 ****************/

///////////
//GLOBALS//
///////////

var _workers = [];

//////////////////
//PUBLIC METHODS//
//////////////////

///Creates a new worker.
function createWebWorker(workerId, blazorInstance, createdCallback, resultCallback, errorCallback, eventCallback, settings, token)
{
    //initialization
    if (!blazorInstance || !createdCallback)
    {
        //error
        handleError('A Blazor instance with a creation callback is required for web workers.');
    }

    //create worker
    var worker = new Worker('/_content/LinesOfCode.Web.Workers/web-worker-instance.js');
    worker.postMessage = worker.webkitPostMessage || worker.postMessage;

    //hook message event
    worker.addEventListener('message', function (e)
    {
        //initialization
        if (e == null || e.data == null)
        {
            //unknown message
            var message =
            {
                //assemble object
                error: 'Unable to call finish an invocation: An empty result was received.'
            }

            //return
            blazorInstance.invokeMethodAsync(errorCallback, message);
            return;
        }

        //listen for initialization events
        if (e.data === 'started')
        {
            //worker started
            console.log('Worker ' + workerId + ' has been started.');
        }
        else if (e.data === 'loaded')
        {
            //worker loaded
            console.log('Worker ' + workerId + ' has been loaded.');
            blazorInstance.invokeMethodAsync(createdCallback, workerId);
        }
        else if (e.data === 'token')
        {
            //worker authenticated
            console.log('Worker ' + workerId + ' has been authenticated.');
        }
        else
        {
            //process message
            var json = arrayBufferToString(e.data);
            var message = JSON.parse(json);
            switch (message.command)
            {
                //error
                case 'error':

                    //return
                    blazorInstance.invokeMethodAsync(errorCallback, message);
                    break;

                //event
                case 'event':

                    //return
                    blazorInstance.invokeMethodAsync(eventCallback, message);
                    break;

                //invocation result
                case 'result':

                    //return
                    blazorInstance.invokeMethodAsync(resultCallback, message);
                    break;

                //unknown command
                default:

                    //unknown message
                    var error =
                    {
                        //assemble object
                        proxy: message.proxy,
                        invocationId: message.invocationId,
                        error: 'Unable to call finish an invocation: Unknown command ' + message.command + '.'
                    }

                    //return
                    blazorInstance.invokeMethodAsync(errorCallback, error);
                    break;
            }
        }
    }, false);

    //start worker
    var message =
    {
        //assemble object
        token: token,
        settings: stringToArrayBuffer(JSON.stringify(settings))
    };

    //return
    worker.postMessage(message, [message.settings.buffer]);
    this._workers.push(
        {
            //assemble object
            id: workerId,
            instance: worker
        });
}

//Sends an auth token to a web worker.
function sendWebWorkerToken(workerId, token)
{
    //initialization
    var worker = this.getWorkerById(workerId);
    if (!worker)
    {
        //error
        handleError('Web worker ' + workerId + ' was not found.');
        return;
    }

    //build message
    var message =
    {
        //assemble object        
        token: token,
        command: 'token',
        invocationId: workerId
    };

    //return
    var json = JSON.stringify(message);
    var transferrable = stringToArrayBuffer(json);
    worker.postMessage(transferrable, [transferrable.buffer]);
}

///Calls a method inside a worker.
function invokeWorker(workerId, invocationId, proxy, eventRegistrations, fileUploadControlId)
{
    //initialization
    var worker = this.getWorkerById(workerId);
    if (!worker)
    {
        //error
        handleError('Web worker ' + workerId + ' was not found.');
        return;
    }

    //build message
    var message =
    {
        //assemble object        
        proxy: proxy,
        command: 'method',
        invocationId: invocationId,
        eventRegistrations: eventRegistrations
    };

    //serialize message
    var json = JSON.stringify(message);
    var messages = [stringToArrayBuffer(json)];

    //add transferrable objects
    if (fileUploadControlId)
    {
        //include uploaded files to process
        var fileUpload = document.getElementById(fileUploadControlId);
        if (fileUpload)
            for (var f = 0; f < fileUpload.files.length; f++)
                messages.push(fileUpload.files[f]);
    }

    //send message
    worker.postMessage(messages, [messages[0].buffer]);

    //return
    console.log('Started invocation ' + invocationId + ' on worker instance ' + workerId + '.');
}

///Terminates a worker.
function terminateWebWorker(workerId)
{
    //initialization
    var worker = this.getWorkerById(workerId);
    if (worker)
    {
        //return
        worker.terminate();
        console.log('Terminated worker instance ' + workerId + '.');
        this._workers = this._workers.filter(w => w.id !== workerId);
    }
}

///////////////////
//PRIVATE METHODS//
///////////////////

///Gets a worker by id.
function getWorkerById(id)
{
    //initialization
    if (!this._workers)
    {
        //error
        handleError('Workers have not been initialized.');
        return;
    }

    //get worker
    var workers = this._workers.filter(w => w.id === id);
    if (workers && workers.length > 0)
    {
        //get worker instance
        var result = workers[0].instance;
        if (result)
        {
            //return
            console.log('Found worker instance ' + id + '.');
            return result;
        }
        else
        {
            //error
            handleError('Worker instance ' + id + ' was not found.');
            return null;
        }
    }
    else
    {
        //error
        handleError('Worker ' + id + ' was not found.');
        return null;
    }
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