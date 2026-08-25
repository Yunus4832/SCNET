# Server-capacity experiments

## Capacity definition

Define capacity as the highest sustained client count for a specified workload that meets all declared
service objectives. Record hardware, runtime build, world, mods, visibility, entity density, client
behavior, duration, and network profile.

Minimum service objectives should cover:

- Headless tick timeliness and long-frame frequency;
- CPU and working-set memory;
- disconnect or protocol error count;
- reliable-send backlog or proxy queue growth when available;
- chunk convergence time and hit-response latency for at least one real canary client.

## Do not launch many full GUI clients

Use a layered population:

1. Keep one real GUI canary client to validate user-visible behavior and full protocol correctness.
2. Add lightweight protocol load clients for connection, player state, movement snapshots, chunk
   requests, and selected gameplay events. They must follow the real bootstrap and package contracts;
   raw UDP packet replay is unsuitable for capacity claims because connection and reliable-delivery
   state are live.
3. Validate the load client against two to four real clients before using it at larger counts.

A lightweight load client measures server network/gameplay cost without rendering, audio, geometry,
or full client simulation. Report that boundary. Entity-heavy behavior may still require server-side
scenario setup because idle synthetic clients understate gameplay load.

## Step-load method

Use steps such as 1, 2, 4, 8, 16, then smaller increments near saturation. Hold each step long enough
for chunk bootstrap and queues to settle. Abort a step when memory grows without a bound, the server
misses the declared tick objective, disconnects begin, or queues keep increasing after arrivals stop.

Run at least these workload classes separately:

- connected-idle: connection and heartbeat floor;
- distributed-exploration: distinct chunk requests and worst-case bandwidth/encoding;
- shared-area movement: cache-friendly chunk reuse plus player/body broadcasts;
- combat/event burst: reliable event and hit-processing pressure.

Do not average these workloads into one maximum. The lowest capacity among supported workload classes
is the defensible operational limit.

## Evidence and interpretation

Sample server CPU, working set, threads, and process uptime at least once per second. Prefer in-game
tick and network counters when implemented; operating-system CPU alone cannot distinguish simulation,
encoding, serialization, and send pressure.

Plot or tabulate client count against CPU, memory, tick lateness, outbound bandwidth, chunk queue peak,
and P95 canary latency. Capacity is the knee before objectives fail, not the last count before a crash.

Until a protocol load client and server metrics exist, report capacity as unknown and provide only a
baseline resource measurement. Never extrapolate a maximum from one or two clients.
