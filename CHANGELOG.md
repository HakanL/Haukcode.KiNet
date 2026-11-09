# Changelog

The package version is major.minor from version.json plus the CI run number, so
every push to main publishes a new patch version. Breaking changes bump the
major version.

## 1.0

- The package embeds its PDB with SourceLink, so the debugger can step into
  the library source (1.0.63 and later).
- Performance: sending sharded by universe and no per-packet allocations on
  the send path.
- A send-immediately method.
- .NET 8, .NET 9 and .NET 10 support.
- Rewritten on the shared Haukcode.HighPerfComm client.
- 1.0.0 - Initial release
- Unicast output leaves through the network interface that owns the local
  address, not whichever one the routing table prefers. On Linux a host with
  two interfaces on one subnet, or with the other interface holding the lower
  route metric, sent unicast from the wrong interface (needs Haukcode.Network
  1.1.32, Utils.BindToInterfaceOfAddress).
