'use strict';

/******************
 *WEB WORKER PROXY*
 ******************/

//////////////////
//INITIALIZATION//
//////////////////

import * as common from '/_content/LinesOfCode.Web.Workers/web-worker-common.js';

//////////////////
//PUBLIC METHODS//
//////////////////

///This sets the blazor callback for this worker and tells the UI thread that it is ready.
export function connectWebWorker(blazorReference, callbackMethod, tokenMethod, refreshMethod)
{
    //initialization
    globalThis.tokenMethod = tokenMethod;
    globalThis.refreshMethod = refreshMethod;
    globalThis.callbackMethod = callbackMethod;
    globalThis.blazorReference = blazorReference;

    //hook web worker message event
    globalThis.postMessage = globalThis.webkitPostMessage || globalThis.postMessage;
    globalThis.addEventListener('message', function (e)
    {
        //initialization
        if (e?.data == null)
        {
            //error
            common.handleError('A web worker received an empty message.');
        }
        else
        {
            //get message
            var hasFiles = Array.isArray(e.data);
            var json = hasFiles ? common.arrayBufferToString(e.data[0]) : common.arrayBufferToString(e.data);

            //check message
            if (!json || json.length === 0)
            {
                //error
                common.handleError(`A web worker message could not be parsed: ${e.data}`);
                return;
            }

            //parse message
            var message = JSON.parse(json);
            console.log(`Processing web worker command ${message.command}.`);

            //check command
            switch (message.command)
            {
                //call a method
                case 'method':

                    //check files
                    if (hasFiles)
                    {
                        //get files
                        _files = [];
                        for (var f = 1; f < e.data.length; f++)
                            _files.push(e.data[f]);

                        //files acquired
                        console.log(`Acquired ${_files.length} file(s).`);
                    }

                    //call blazor method
                    console.log(`Performing method invocation ${message.invocationId}.`);
                    globalThis.blazorReference.invokeMethodAsync(globalThis.callbackMethod, message.invocationId, message.proxy, message.eventRegistrations)
                                              .then((result) =>
                                              {
                                                  //send result to UI thread
                                                  var successMessage =
                                                  {
                                                      //assemble object                                                             
                                                      result: result,
                                                      command: 'result',
                                                      proxy: message.proxy,
                                                      invocationId: message.invocationId
                                                  };

                                                  //return
                                                  mashalMethodResult(successMessage);
                                              }, (error) =>
                                              {
                                                  //initialization
                                                  var failMessage =
                                                  {
                                                      //assemble object
                                                      command: 'error',
                                                      error: error.message,
                                                      proxy: message.proxy,
                                                      invocationId: message.invocationId
                                                  };

                                                  //return
                                                  mashalMethodResult(failMessage);
                                              });
                    break;

                //set auth token
                case 'token':

                    //call blazor method
                    console.log(`Performing authentication for invocation ${message.invocationId}.`);
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
                                                  var transferrable = common.stringToArrayBuffer(json);
                                                  globalThis.postMessage(transferrable, [transferrable.buffer]);
                                              });
                    break;

                //refresh auth token
                case 'refresh':

                    //call blazor method
                    console.log('Performing token refresh.');
                    globalThis.blazorReference.invokeMethodAsync(globalThis.refreshMethod, message.token);

                    //the refresh method is void, so no promises here
                    break;

                //error
                default:
                    common.handleError(`Unknown web worker command for invocation ${message.invocationId}: ${message.command}.`);
                    break;
            }

        }
    }, false);

    //tell the UI thread that this worker has loaded Blazor
    globalThis.postMessage('started');
}

///This sends a message to the UI thread in response to a web worker event being raised.
export function marshalEvent(invocationId, proxy, name, type, value)
{
    //initialization
    var eventMessage =
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
    mashalMethodResult(eventMessage);
}

///Called by the proxy manager's exception handling logic to attempt to acquire a new auth token.
export function refreshAuthenticationToken()
{
    //initialization
    console.log('Requesting refresh token.');

    //return
    globalThis.postMessage('refresh');
}

///////////////////
//PRIVATE METHODS//
///////////////////

///This sends a message to the UI thread in response to a web worker method completeing or failing.
function mashalMethodResult(message)
{
    //initialization
    var json = JSON.stringify(message);
    var transferrable = common.stringToArrayBuffer(json);

    //return
    globalThis.postMessage(transferrable, [transferrable.buffer]);
}