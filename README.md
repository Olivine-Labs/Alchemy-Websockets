Alchemy Websockets
=============

A websocket server built in C# to be extremely efficient and scalable over high 
numbers of connections.

You can download the compiled binary (any cpu) at [alchemyws](https://sourceforge.net/projects/alchemyws/) on SourceForge.

You can download the client javascript library at [alchemy-websockets-client-library](https://github.com/Olivine-Labs/Alchemy-Websockets-Client-Library)

Documentation: [docs.alchemywebsockets.net](http://docs.alchemywebsockets.net/)

Usage
-------
After compilation, include the dll as a reference. Starting a server is as simple 
as instantiating a server object, opening the connection, and binding events.

An example application can be seen on [alchemy-websockets-example](https://github.com/Olivine-Labs/Alchemy-Websockets-Example)

Example:

	WSServer AServer = new WSServer(81, IPAddress.Any);
	
	AServer.DefaultOnReceive = new OnEventDelegate(OnReceive);
	AServer.DefaultOnSend = new OnEventDelegate(OnSend);
	AServer.DefaultOnConnect = new OnEventDelegate(OnConnect);
	AServer.DefaultOnDisconnect = new OnEventDelegate(OnDisconnect);
	AServer.TimeOut = new TimeSpan(0, 5, 0);
	AServer.Start();


License
-------
Copyright 2011, Olivine Labs, LLC.
Licensed under [LGPL](http://www.gnu.org/licenses/lgpl.html).
