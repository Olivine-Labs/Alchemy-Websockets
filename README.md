Alchemy Websockets
=============

A websocket server built in C# to be extremely efficient and scalable over high
numbers of connections.


You can download the client javascript library at [alchemy-websockets-client-library](https://github.com/Olivine-Labs/Alchemy-Websockets-Client-Library)

Documentation: [docs.alchemywebsockets.net](http://docs.alchemywebsockets.net/)

Usage
-------
Alchemy Websockets is a Visual Studio 2010 project. It can be loaded in the free
Visual C# Express and Monodevelop as well (and potentially other compatible IDEs.)

After compilation, include the dll as a reference into your project. From there,
starting a server is as simple as instantiating an WSServer object, opening the 
connection, and binding events.

An example application can be seen on [alchemy-websockets-example](https://github.com/Olivine-Labs/Alchemy-Websockets-Example)

Example:

        static void Main(string[] args)
        {
        var aServer = new WSServer(8100, IPAddress.Any)
        {
            DefaultOnReceive = new OnEventDelegate(OnReceive),
            DefaultOnSend = new OnEventDelegate(OnSend),
            DefaultOnConnect = new OnEventDelegate(OnConnect),
            DefaultOnDisconnect = new OnEventDelegate(OnDisconnect),
            TimeOut = new TimeSpan(0, 5, 0)
        };

        aServer.Start();
        }

        static void OnConnected(UserContext aContext)
        {
          Console.WriteLine("Client Connection From : " + aContext.ClientAddress.ToString());
        }

        //...etc

Documentation
-------------
Check out our documentaion at [http://olivinelabs.com/Alchemy-Websockets](http://olivinelabs.com/Alchemy-Websockets)

Status
------
Alchemy websockets supports:

* (!) hixie-76 (hybi00)
* (✓) hybi-10
* (✓) hybi-17 (official protocol)

_hixie-76 has open issues._
Because it is on the official support list (due to Safari), it is on the list to be resolved.

This covers Chrome current, beta, and alpha channels; Firefox
latest, beta, and alpha channels; Safari, and includes support
for a flash-based socket fallback for browsers with no sorkcet
support natively.

[Browser Support List](http://en.wikipedia.org/wiki/WebSocket#Browser_support)

License
-------
Copyright 2011, Olivine Labs, LLC.
Licensed under [LGPL](http://www.gnu.org/licenses/lgpl.html) and
[MIT](http://www.opensource.org/licenses/mit-license.php).
