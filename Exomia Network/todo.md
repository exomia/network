# Todo

- [x] add a disconnect reason [graceful, aborted, error, unspecified] to all disconnect handlers
- [x] use a flag system (Interlocked/atomic to set states of the socket like sending or disconnected that you cant send anymore instead of null check of socket)
- [ ] unit tests
- [ ] renaming / move classes like ('Serilization')
- [ ] 'DELEGATES.cs' remove
- [ ] performance measurement / benchmarking

## Features

### Async IO

- [ ] build client & server classes which use \*\*\*Async instead of Begin\*\*\* and End\*\*\*

### Security

- [ ] start a connection with 'ssl' or 'end to end' encryption instead of 'unsecure'
- [ ] upgrade a existing 'unsecure' connection to use 'ssl' or 'end to end' for communication
- [ ] switch between 'unsecure', 'ssl' and 'end to end'


## Bugs

- [ ] wrong disconnect messages


## Viewer Requests

- [ ] "den quadrat umherschieber und server dazu anlegen!" (copiussole 22.Aug.2018)
