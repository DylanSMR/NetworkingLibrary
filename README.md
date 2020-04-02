# Networking Library
 A basic low-mid level networking library for unity designed to be highly customizable and work with proxys, local hosting, and singleplayer type games.

# Documentation
Eventually i'll have a documentation folder/website to explain what everything does and how to use it at a basic level

# Todo (Networking Library)
### Backburner
* Better logging, advanced errors, etc
* Rework the settings system, make it easier to use or just get rid of it
### Researching
* Use reflection to use RPC attributes on functions to clean up code, need to figure out if its fast enough
* Better ways to send data across the network that take up less bytes. JSON is slightly wasteful, possibly protobuf?
* Find a way to introduce lag compenstation into the transform system. Currently, it is too laggy if the server keeps sending the authorative client back a position 100ms later for example
* Ways to implement packets that are always going to reach their destination. Possibly implement TCP into as well
### In Progress
* Clean up rpc system
* Implement networked scene switching
* Create a proxy server that can be ran to connect clients and a server in multiple languages
  * - [ ] Python
  * - [ ] C# 
  * - [ ] C++

# Todo (Network Library Proxy)
### Researching
* Better ways to send/receive packets, possibly using multiple threads?
* Colors in console, colors make everything better
### In Progress
* Add debug statistics such as packets received, sent, average packet size, etc
