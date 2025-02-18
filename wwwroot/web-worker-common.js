'use strict';

/*******************
 *WEB WORKER COMMON*
 *******************/

//////////////////
//PUBLIC METHODS//
//////////////////

///Encodes a string as a UTF-8 buffer.
export function stringToArrayBuffer(value)
{
    //initialization
    var encoder = new TextEncoder();

    //return
    return encoder.encode(value);
}

///This converts a UTF-8 array buffer to a string.
export function arrayBufferToString(buffer)
{
    //initialization
    var decoder = new TextDecoder();
    var array = new Uint8Array(buffer);

    //return
    return decoder.decode(array);
}

///Provides a basic error logging wrapper.
export function handleError(errorMessage)
{
    //return
    console.error(errorMessage);
}