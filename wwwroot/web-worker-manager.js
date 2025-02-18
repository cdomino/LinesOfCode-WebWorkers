/****************
 *WORKER MANAGER*
 ****************/

//////////////////
//INITIALIZATION//
//////////////////

export var _workers = [];
import * as common from '/_content/LinesOfCode.Web.Workers/web-worker-common.js';

//////////////////
//PUBLIC METHODS//
//////////////////

///Creates a new worker.
export function createWebWorker(workerId, blazorInstance, createdCallback, resultCallback, errorCallback, eventCallback, tokenCallback, settings, token)
{
    //initialization
    if (!blazorInstance || !createdCallback)
    {
        //error
        common.handleError('A Blazor instance with a creation callback is required for web workers.');
        return;
    }

    //create web worker instance (which can't be a module since Blazor needs to be loaded into a "normal" JavaScript context)
    var worker = new Worker('/_content/LinesOfCode.Web.Workers/web-worker-instance.js');
    worker.postMessage = worker.webkitPostMessage || worker.postMessage;

    //hook message event
    worker.addEventListener('message', function (e)
    {
        //initialization
        if (e?.data == null)
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
            //worker loaded
            console.log(`Worker ${workerId} has been loaded.`);
            blazorInstance.invokeMethodAsync(createdCallback, workerId);
        }       
        else if (e.data === 'token')
        {
            //worker authenticated
            console.log(`Worker ${workerId} has been authenticated.`);
        }
        else if (e.data === 'refresh')
        {
            //worker requested a token refresh
            console.log(`Worker ${workerId} has requested a refreshed token.`);
            blazorInstance.invokeMethodAsync(tokenCallback, message)
                          .then((token) =>
                          {
                              //check token
                              if (!token || !token.secret || token.secret.length === 0)
                              {
                                  //no token received
                                  common.handleError(`Unable to acquire refresh token for worker ${workerId} token not found.`);
                              }
                              else
                              {
                                  //wrap token in a message
                                  var refreshToken =
                                  {
                                      //assemble object
                                      command: 'refresh',
                                      token: token.secret
                                  };

                                  //send new token back to worker
                                  var transferrable = common.stringToArrayBuffer(JSON.stringify(refreshToken));
                                  worker.postMessage(transferrable, [transferrable.buffer]);

                                  //return
                                  console.log(`Worker ${workerId} has recieved a refreshed token.`);
                              }
                          }, (error) =>
                          {
                              //error
                              common.handleError(`Unable to acquire refresh token for worker ${workerId}: ${error}`);
                          });
        }
        else
        {
            //process message
            var json = common.arrayBufferToString(e.data);
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
                        error: `Unable to call finish an invocation: Unknown command ${message.command}.`
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
        settings: common.stringToArrayBuffer(JSON.stringify(settings))
    };

    //return
    worker.postMessage(message, [message.settings.buffer]);
    _workers.push(
    {
        //assemble object
        id: workerId,
        instance: worker
    });
}

//Sends an auth token to a web worker.
export function sendWebWorkerToken(workerId, token)
{
    //initialization
    var worker = getWorkerById(workerId);
    if (!worker)
    {
        //error
        common.handleError(`Web worker ${workerId} was not found.`);
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
    var transferrable = common.stringToArrayBuffer(json);
    worker.postMessage(transferrable, [transferrable.buffer]);
}

///Calls a method inside a web worker.
export function invokeWebWorker(workerId, invocationId, proxy, eventRegistrations, fileUploadControlId)
{
    //initialization
    var worker = getWorkerById(workerId);
    if (!worker)
    {
        //error
        common.handleError(`Web worker ${workerId} was not found.`);
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
    var messages = [common.stringToArrayBuffer(json)];

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
    console.log(`Started invocation ${invocationId} on worker instance ${workerId}.`);
}

///Terminates a worker.
export function terminateWebWorker(workerId)
{
    //initialization
    var worker = getWorkerById(workerId);
    if (worker)
    {
        //return
        worker.terminate();
        console.log(`Terminated worker instance ${workerId}.`);
        _workers = _workers.filter(w => w.id !== workerId);
    }
}

///Gets a worker by id.
export function getWorkerById(id)
{
    //initialization
    if (!_workers)
    {
        //error
        common.handleError('Workers have not been initialized.');
        return;
    }

    //get worker
    var workers = _workers.filter(w => w.id === id);
    if (workers && workers.length > 0)
    {
        //get worker instance
        var worker = workers[0].instance;
        if (worker)
        {
            //return
            console.log(`Found worker instance ${id}.`);
            return worker;
        }
        else
        {
            //error
            common.handleError(`Worker instance ${id} was not found.`);
            return null;
        }
    }
    else
    {
        //error
        common.handleError(`Worker ${id} was not found.`);
        return null;
    }
}