﻿'use strict';

/************************
 *WEB WORKER ENVIRONMENT*
 ************************/

///////////
//MEMBERS//
///////////

//Blazor requires a Comment class to exist.
class Comment { }

//This represents an object with a property bag.
class PropertyBag
{
    setProperty(key, value)
    {
        //noop
    }
}

//Blazor requires a Node class to exist; this contains the bare minimum functionality that Blazor expects to be in the DOM.
class Node
{
    constructor()
    {
        //initialization
        this.childNodes = [];
    }

    appendChild(node)
    {
        //initialization
        node.parentNode = this;
        this.childNodes.push(node);

        //return
        return node;
    }

    hasChildNodes()
    {
        //return
        return this.childNodes.length > 0;
    }

    getRootNode()
    {
        //return
        return document;
    }

    removeChild(node)
    {
        //noop
    }

    addEventListener(type, callback, options)
    {
        //noop
    }    
}

//Blazor requires an Element class to exist; this adds attribution.
class Element extends Node
{
    constructor()
    {
        //initialization
        super();
    }

    getAttribute(name)
    {
        //return
        return this[name];
    }

    setAttribute(name, value)
    {
        //return
        this[name] = value;
    }
}

//Blazor requires an HTMLElement class to exist; this adds styles.
class HTMLElement extends Element
{
    constructor()
    {
        //initialization
        super();
        this.style = createProxy(new PropertyBag());
    }
}

//This represents a script elment.
class Script extends Element
{
    constructor()
    {
        //initialization
        super();
        this.src = null;
    }
}

//This represents the DOM, which is really only needed to "host" the Blazor script element.
class DOM extends Node
{
    constructor()
    {
        //initialization
        super();
        this.body = null;
    }

    get documentElement()
    {
        //return
        if (this.hasChildNodes)
            return this.childNodes[0];
        else
            return null;
    }

    createElement(tagName)
    {
        //initialization
        var element = tagName.toLowerCase() === 'script' ? new Script() : new HTMLElement();
        element.tagName = tagName.toUpperCase();

        //return
        return element;
    }

    createElementNS(namespaceURI, tagName)
    {
        //initialization
        var element = this.createElement(tagName);
        element.namespaceURI = namespaceURI;

        //return
        return element;
    }

    createComment(comment)
    {
        //return
        return new Comment(comment);
    }

    querySelector(selectors)
    {
        //return
        return this.body;
    }

    querySelectorAll(selectors)
    {
        //return
        return [this.body];
    }

    //This exposes the "native" location object as required by Blazor.
    get location()
    {
        //return
        return location;
    }   

    //This loads the Blazor script.
    loadBlazor()
    {
        //initialization
        this.body = this.childNodes[0];

        //return
        var scriptURL = new URL(this.body.childNodes[0].src, this.baseURI);
        importScripts(scriptURL);
    }
}

///////////////////
//PUBLIC METHODS//
///////////////////

//This creates instances of mocked objects to satisfy Blazor JavaScript calls in a non-DOM context.
function createProxy(instance, handler)
{
    //return
    return new Proxy(instance,
                    {
                        get(target, key)
                        {
                            //return
                            if (handler && typeof handler[key] !== 'undefined')
                                return handler[key];
                            else
                                return target[key];
                        },

                        set(target, key, value)
                        {
                            //initialization
                            target[key] = value;

                            //return
                            return true;
                        }
                    });
}