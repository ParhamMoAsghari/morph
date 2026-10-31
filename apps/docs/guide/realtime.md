# Realtime

Morph exposes a SignalR hub for entity change fan-out. Channel authorization goes through Access.

## Hub

Path is published on Host root JSON (`realtime`) and in `Morph.Runtime.Api` (`RealtimeChannels.HubPath`).

## Status

- Authorizer unit tests cover channel rules
- End-to-end Host hub load is a known **Partial/Gap** in the validity matrix — prefer REST polling or webhooks for critical paths until hub e2e lands

Connect with a JWT-capable SignalR client and subscribe only to channels your principal may read.
