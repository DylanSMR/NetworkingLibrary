# Networking Library
 A basic low-mid level networking library for unity designed to be highly customizable and work with proxys, local hosting, and singleplayer type games.

# Documentation
Eventually i'll have a documentation folder/website to explain what everything does and how to use it at a basic level

# Todo (Networking Library)
### Backburner
* Better logging, advanced errors, etc
* Implement networked scene switching
* Rework the settings system, make it easier to use or just get rid of it
* Clean up rpc system
* Implement better debugging screen to show advanced statistics.
  * Ping, Average ping, Ping to proxy if available?
  * Frames received/sent count
  * Current players connected, amount of networked objects vs unnetworked objects in scene
  * Anything else that I can get my hands on
### Researching
* Use reflection to use RPC attributes on functions to clean up code, need to figure out if its fast enough
* Better ways to send data across the network that take up less bytes. JSON is slightly wasteful, possibly protobuf?
* Find a way to introduce lag compenstation into the transform system. Currently, it is too laggy if the server keeps sending the authorative client back a position 100ms later for example
* If it would be more efficient to use both a UDP and TCP client, instead of having the "important frame" marker within udp
### In Progress
* Create a proxy server that can be ran to connect clients and a server in multiple languages
  * - [x] Python
  * - [ ] C# 
  * - [ ] C++
* Automatic load-balanced to resolve issues where the UdpClient is filling up with so much data that one thread can not handle it
* Clean code up, possibly rework most functions

# Todo (Network Library Proxy)
### Researching
* Better ways to send/receive packets, possibly using multiple threads?
* Colors in console, colors make everything better
### In Progress
* Add debug statistics such as packets received, sent, average packet size, etc
