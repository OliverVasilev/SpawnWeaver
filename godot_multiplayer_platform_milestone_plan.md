# Godot Multiplayer-as-a-Service Platform — Improvement Milestone Plan

## Context

We are building a **Godot-first multiplayer-as-a-service platform**.

The platform already has an MVP/public alpha foundation:

- Repository/bootstrap completed
- Domain model and project registration
- WebSocket connection gateway
- Protocol envelope
- Basic rooms
- Godot GDScript SDK MVP
- Game event relay
- Local playtest infrastructure
- Authentication v1
- Lobby system
- Matchmaking v1
- Persistence v1
- Dashboard v1
- Observability and diagnostics
- Performance pass v1
- SDK developer experience pass
- Security pass v1
- Public Alpha

Current dashboard state:

- There is a starting landing page
- Users can create a project
- More dashboard/account/product structure is still needed

The next goal is to **differentiate the platform from Photon, Unity Gaming Services, PlayFab, Nakama, Colyseus, and other multiplayer/backend providers** by being:

> The fastest and friendliest way for Godot developers to add online multiplayer, lobbies, matchmaking, persistence, and simple live state sync without building backend infrastructure.

This plan should improve the platform around seven pillars:

1. Godot-native SDK polish
2. Full working example projects
3. Multiplayer debugger/dashboard
4. Simple state sync v1
5. Godot editor plugin
6. Excellent docs/tutorials
7. Simple indie pricing

Note: Test each feature before moving on to the next one
---

# Product Positioning

## Core Positioning

The platform should not try to be a full enterprise backend suite immediately.

It should be positioned as:

> A Godot-native multiplayer backend for indie developers who want lobbies, matchmaking, rooms, realtime events, persistence, and simple state sync with minimal backend work.

## Target User

Primary user:

- Indie Godot developer
- Solo developer or small team
- Wants to add multiplayer to a prototype or small commercial game
- Does not want to build and operate custom backend infrastructure
- Wants simple pricing and predictable limits
- Needs examples, templates, and debugging tools

## Product Promise

The product should make this possible:

> From single-player Godot prototype to working online multiplayer in one afternoon.

## What the Product Should Be Good At

Support these use cases well:

- Multiplayer lobbies
- Ready checks
- Room-based multiplayer
- Casual co-op
- Small arena games
- Turn-based games
- Party games
- Lightweight realtime games
- Player profile persistence
- Inventory/progression persistence
- Simple live room/entity state
- Late-join snapshots
- Debugging failed multiplayer sessions

## What the Product Should Not Claim Yet

Do not market the product as a complete solution for:

- Competitive shooters
- Full MMO infrastructure
- Advanced anti-cheat
- Server-authoritative physics
- Rollback netcode
- Large-world replication
- Voice chat
- Full LiveOps suite
- Complex economy platform

---

# Milestone 19 — Account System & Product Onboarding

## Goal

Create a complete account and onboarding flow so developers can sign up, create organizations/projects, choose a game type, and receive a recommended setup path.

The current dashboard only has a starting landing page and project creation. This milestone turns it into a real product entry point.

## Why This Matters

Before improving advanced multiplayer features, the platform needs a strong first-user experience.

A new developer should immediately understand:

- What the service does
- What type of game they are building
- Which features they need
- How to create a project
- How to connect their Godot game
- What their next step is

## Deliverables

### 19.1 User Accounts

Implement user accounts for dashboard access.

Required:

- Sign up
- Sign in
- Sign out
- Password reset if using password auth
- Email verification if applicable
- Session management
- Basic account settings page

Recommended account fields:

```text
User
- Id
- Email
- DisplayName
- CreatedAt
- UpdatedAt
- LastLoginAt
```

### 19.2 Organizations / Workspaces

Support organizations or workspaces, even if each user starts with only one.

Required:

```text
Organization
- Id
- Name
- OwnerUserId
- CreatedAt
- UpdatedAt
```

Recommended:

- One user can belong to multiple organizations later
- One organization can own multiple projects
- Keep team invites optional for now

### 19.3 Project Creation v2

Improve project creation.

Current:

- User can create a project

Add:

- Project name
- Game type selection
- Target platform
- Expected multiplayer type
- Persistence requirements
- Suggested feature setup

Recommended project fields:

```text
Project
- Id
- OrganizationId
- Name
- Slug
- GameType
- MultiplayerMode
- PersistenceMode
- Environment
- ApiKeyPublic
- ApiSecretHash
- CreatedAt
- UpdatedAt
```

### 19.4 Game Type Selection

During project creation, ask:

```text
What type of game are you building?
```

Options:

- Turn-based
- Casual co-op
- 1v1 arena
- Small party game
- Lobby-based game
- Realtime action
- Persistent progression game
- Other

Store this as `GameType`.

### 19.5 Multiplayer Mode Selection

Ask:

```text
What multiplayer style do you need?
```

Options:

- Rooms only
- Lobbies + rooms
- Matchmaking + rooms
- Event relay only
- State sync
- Persistence only
- Not sure

Store this as `MultiplayerMode`.

### 19.6 Persistence Selection

Ask:

```text
What data do you need to save?
```

Options:

- No persistence yet
- Player profile
- Inventory
- Progression/XP
- Match history
- Save slots
- Custom key-value data

Store this as `PersistenceMode` or `PersistenceFeatures`.

### 19.7 Recommended Setup Screen

After project creation, show a setup checklist based on the selected game type.

Example for a 1v1 arena game:

```text
Recommended setup:
- Enable matchmaking
- Enable room events
- Enable entity state sync
- Enable player profile persistence
- Install Godot SDK
- Run 1v1 arena example
```

Example for a turn-based game:

```text
Recommended setup:
- Enable rooms
- Enable room state
- Enable persistence
- Use turn-based example project
```

## Acceptance Criteria

- A developer can create an account
- A developer can create a project under an organization
- Project creation asks for game type, multiplayer mode, and persistence needs
- Dashboard recommends a setup path after project creation
- The recommended setup changes based on selected game type
- The project has visible API credentials and SDK setup instructions

---

# Milestone 20 — Godot-Native SDK Polish

## Goal

Make the SDK feel like a native Godot addon rather than a generic WebSocket client.

## Why This Matters

The strongest differentiation is being Godot-first.

The SDK should feel natural to Godot developers through:

- Signals
- Nodes
- Autoload support
- Inspector configuration
- Simple async methods
- Clear error handling
- Good examples

## Deliverables

### 20.1 SDK Structure

Create a clear SDK structure:

```text
addons/
  godot_multiplayer_service/
    plugin.cfg
    GodotNet.gd
    nodes/
      MultiplayerClient.gd
      LobbyClient.gd
      MatchmakingClient.gd
      StateSyncClient.gd
      PersistenceClient.gd
    examples/
    icons/
```

### 20.2 Main Autoload Client

Expose a simple primary client.

Example:

```gdscript
extends Node

func _ready():
    GodotNet.connected.connect(_on_connected)
    GodotNet.room_joined.connect(_on_room_joined)
    GodotNet.entity_state_changed.connect(_on_entity_state_changed)

    await GodotNet.configure({
        "project_id": "PROJECT_ID",
        "public_key": "PUBLIC_KEY"
    })

    await GodotNet.connect()
    await GodotNet.login_guest()
```

### 20.3 Godot Signals

Expose signals for all important events:

```gdscript
signal connected()
signal disconnected(reason)
signal authenticated(player)
signal auth_failed(error)

signal room_joined(room)
signal room_left(room_id)
signal room_event_received(event_name, payload, sender)

signal lobby_created(lobby)
signal lobby_joined(lobby)
signal lobby_updated(lobby)
signal lobby_left(lobby_id)

signal matchmaking_started(ticket)
signal matchmaking_matched(match)
signal matchmaking_cancelled(ticket_id)
signal matchmaking_failed(error)

signal state_snapshot_received(snapshot)
signal room_state_changed(patch)
signal entity_state_changed(entity_id, patch, full_state)
signal entity_state_deleted(entity_id)

signal persistence_loaded(key, value)
signal persistence_saved(key)
signal sdk_error(error)
```

### 20.4 Error Model

Create a consistent SDK error shape:

```gdscript
{
    "code": "ROOM_NOT_FOUND",
    "message": "Room does not exist.",
    "details": {},
    "retryable": false
}
```

SDK errors should be:

- Human-readable
- Machine-readable
- Documented
- Visible in dashboard debugger

### 20.5 Reconnection Behavior

Implement SDK reconnection support.

Required:

- Heartbeat timeout detection
- Automatic reconnect option
- Manual reconnect option
- Re-auth after reconnect
- Optional room rejoin
- Clear reconnect signals

Signals:

```gdscript
signal reconnecting(attempt)
signal reconnected()
signal reconnect_failed(reason)
```

### 20.6 SDK Debug Mode

Add debug logging.

Example:

```gdscript
GodotNet.set_debug_enabled(true)
```

Debug mode should log:

- Connection state
- Auth flow
- Room/lobby/matchmaking actions
- Incoming/outgoing messages
- State sync patches
- Rejected messages
- Disconnect reasons

### 20.7 Copy Debug Bundle

Add SDK method:

```gdscript
var report = GodotNet.create_debug_report()
```

The report should include:

- SDK version
- Godot version
- Project ID
- Current connection state
- Player ID
- Room ID
- Lobby ID
- Last errors
- Last 50 protocol messages
- Ping/latency stats

## Acceptance Criteria

- SDK can be installed as a Godot addon
- Developer can connect, authenticate, join room, send event, and receive event with minimal code
- SDK uses Godot signals consistently
- SDK exposes debug mode
- SDK supports reconnect behavior
- SDK can generate a copyable debug report

---

# Milestone 21 — Full Working Example Projects

## Goal

Create complete Godot example projects that demonstrate real use cases end-to-end.

## Why This Matters

Example projects will sell the product better than feature lists.

Developers should be able to clone an example, enter project credentials, and run multiplayer quickly.

## Deliverables

### 21.1 Example Project: Realtime Chat Room

Demonstrates:

- Connect
- Guest auth
- Create room
- Join room
- Send room event
- Receive room event
- Display connected players

### 21.2 Example Project: Lobby + Ready Check

Demonstrates:

- Create lobby
- Join lobby
- List players
- Toggle ready status
- Lobby state updates
- Host starts game
- Transition to room

### 21.3 Example Project: 1v1 Matchmaking Arena

Demonstrates:

- Login as guest
- Enter matchmaking queue
- Match two players
- Join generated room
- Send movement events or state patches
- End match

### 21.4 Example Project: Simple State Sync Demo

Demonstrates:

- Spawn entity
- Own entity
- Patch entity state
- Receive entity state changes
- Late join snapshot
- Delete entity

### 21.5 Example Project: Persistent Player Profile

Demonstrates:

- Save player display name
- Save XP
- Save inventory item
- Load profile on login
- Display saved state in UI

### 21.6 Example Project READMEs

Each example should include:

- What the example teaches
- Required platform features
- Dashboard setup steps
- Godot setup steps
- How to run two clients locally
- Common errors
- Troubleshooting

## Acceptance Criteria

- At least 3 example projects are complete
- Each example can run with two local clients
- Each example has a README
- Each example links to relevant docs
- Dashboard onboarding points users to the correct example based on game type

---

# Milestone 22 — Multiplayer Debugger Dashboard

## Goal

Build a dashboard debugger that makes multiplayer failures easy to understand.

## Why This Matters

Debugging is one of the best ways to distinguish the product.

The platform should answer:

> Why did this player fail to connect, join, match, sync, or persist data?

## Deliverables

### 22.1 Project Overview Dashboard

Show:

- Active connections
- Current rooms
- Current lobbies
- Matchmaking queue size
- Events per minute
- State sync updates per minute
- Persistence reads/writes
- Error rate
- Disconnect rate

### 22.2 Connection Inspector

For each connection/session, show:

```text
Session
- Session ID
- Player ID
- Connected at
- Last heartbeat
- IP/region if available
- SDK version
- Godot version
- Current room
- Current lobby
- Auth status
- Disconnect reason
```

### 22.3 Timeline View

Show a chronological event timeline:

```text
10:01:03 connected
10:01:04 authenticated as guest
10:01:06 joined lobby lobby_88
10:01:10 entered matchmaking queue duel_1v1
10:01:15 matched with player_456
10:01:16 joined room room_abc
10:01:17 received snapshot with 4 entities
10:01:22 rejected state update: entity not owned by player
10:01:30 disconnected: heartbeat timeout
```

### 22.4 Room Inspector

For each room, show:

- Room ID
- Project ID
- Created at
- Players
- Host/owner
- Room metadata
- Room state
- Entity count
- Events per minute
- State patches per minute
- Recent events

### 22.5 Lobby Inspector

For each lobby, show:

- Lobby ID
- Lobby name
- Host
- Players
- Ready status
- Lobby metadata
- Visibility
- Max players
- Recent lobby events

### 22.6 Matchmaking Inspector

Show:

- Active queues
- Tickets in queue
- Average wait time
- Failed matches
- Cancelled tickets
- Created matches
- Match rules used

### 22.7 Persistence Inspector

Show:

- Player data keys
- Project data keys
- Reads/writes
- Failed operations
- Storage usage estimate
- Last updated timestamps

### 22.8 Error Explorer

Aggregate common errors:

- Auth failures
- Invalid project key
- Room not found
- Lobby full
- Matchmaking timeout
- State update rejected
- Persistence quota exceeded
- Rate limited
- Heartbeat timeout

Each error should show:

- Count
- Most recent occurrence
- Affected sessions
- Suggested fix

### 22.9 Debug Bundle Upload/View

Allow SDK debug reports to be pasted/uploaded into dashboard.

Dashboard should parse and display:

- SDK version
- Recent messages
- Last errors
- Connection state
- Room/lobby/matchmaking state

## Acceptance Criteria

- Dashboard shows active sessions, rooms, lobbies, and matchmaking queues
- Developer can inspect a player session timeline
- Developer can inspect room state and entity state
- Developer can see rejected state updates and reasons
- Developer can see common errors and suggested fixes
- SDK debug report can be viewed in dashboard

---

# Milestone 23 — Simple State Sync v1

## Goal

Implement a simple room-scoped live state synchronization layer.

## Why This Matters

Persistence saves data for later.

State sync keeps connected players aligned right now.

This creates a major improvement over raw event relay because late joiners can receive current state instead of missing previous events.

## State Sync v1 Definition

State sync v1 should provide:

```text
Room State
Entity State
Patch Updates
Snapshots on Join
Ownership Rules
Rate Limits
Dashboard Inspection
```

Do not build full authoritative physics, prediction, rollback, or lag compensation in v1.

## Deliverables

### 23.1 Room State

Support shared room-level key-value state.

Example:

```json
{
  "round": 2,
  "phase": "combat",
  "timer": 64,
  "map": "forest"
}
```

SDK:

```gdscript
await GodotNet.patch_room_state({
    "phase": "combat",
    "timer": 64
})
```

### 23.2 Entity State

Support entity-level state.

Example:

```json
{
  "entity_id": "player_123",
  "owner_id": "user_123",
  "state": {
    "x": 120,
    "y": 80,
    "hp": 70,
    "animation": "run"
  }
}
```

SDK:

```gdscript
await GodotNet.set_entity_state("player_123", {
    "x": 120,
    "y": 80,
    "hp": 70
})
```

### 23.3 Patch Updates

Allow partial updates.

SDK:

```gdscript
await GodotNet.patch_entity_state("player_123", {
    "x": 121,
    "y": 82
})
```

### 23.4 Snapshot on Join

When a player joins a room, send:

- Current room state
- Current entity states
- Entity ownership data

Signal:

```gdscript
signal state_snapshot_received(snapshot)
```

### 23.5 Ownership Rules

Start with simple ownership:

```text
- Entity owner can update their entity
- Room host can update room state
- Server rejects unauthorized entity updates
- Server rejects oversized state
- Server rejects rate-limited updates
```

### 23.6 State Deletion

Support deleting entity state.

SDK:

```gdscript
await GodotNet.delete_entity_state("player_123")
```

Signal:

```gdscript
signal entity_state_deleted(entity_id)
```

### 23.7 Limits

Add limits from the start:

```text
Max entities per room
Max state size per entity
Max room state size
Max patch size
Max updates per second per client
Max updates per second per room
```

Suggested initial limits:

```text
Free:
- 50 entities per room
- 4 KB per entity
- 16 KB room state
- 10 state updates/sec/client

Indie:
- 100 entities per room
- 8 KB per entity
- 32 KB room state
- 20 state updates/sec/client

Studio:
- 250 entities per room
- 16 KB per entity
- 64 KB room state
- 30 state updates/sec/client
```

### 23.8 Protocol Messages

Add protocol messages:

```text
state.room.patch
state.room.changed
state.entity.set
state.entity.patch
state.entity.changed
state.entity.delete
state.entity.deleted
state.snapshot
state.update.rejected
```

### 23.9 Dashboard State Inspector

Dashboard should show:

- Current room state
- Entity list
- Entity owner
- Entity state
- Last updated time
- Patch frequency
- Rejected updates

## Acceptance Criteria

- Room state can be patched and broadcast to room members
- Entity state can be created, patched, and deleted
- Late joiners receive full current state snapshot
- Unauthorized entity updates are rejected
- Rate limits exist
- Dashboard can inspect current state
- SDK exposes simple methods and signals

---

# Milestone 24 — Godot Editor Plugin

## Goal

Create a Godot editor plugin that makes setup and testing easier inside the Godot editor.

## Why This Matters

This is a strong Godot-native differentiator.

Instead of making developers jump between dashboard, docs, and code, the plugin should guide them inside Godot.

## Deliverables

### 24.1 Plugin Installation

The plugin should appear in Godot as:

```text
Project > Project Settings > Plugins > Godot Multiplayer Service
```

### 24.2 Configuration Panel

Create an editor dock or panel with:

- Project ID
- Public API key
- Environment selection
- Server URL
- Debug mode toggle
- Test connection button
- Test guest login button

### 24.3 Scene Generator

Add buttons:

```text
Generate Lobby Scene
Generate Matchmaking Scene
Generate Room Chat Scene
Generate State Sync Player Scene
Generate Persistent Profile Scene
```

Generated files could include:

```text
scenes/
  Lobby.tscn
  Matchmaking.tscn
  RoomChat.tscn
  StateSyncPlayer.tscn
scripts/
  Lobby.gd
  Matchmaking.gd
  RoomChat.gd
  StateSyncPlayer.gd
```

### 24.4 Local Multi-Client Launcher

Add a development helper to launch multiple clients.

Features:

- Launch 2 clients
- Launch 4 clients
- Use fake guest users
- Auto-join same room
- Auto-enter matchmaking
- Show logs side-by-side if possible

### 24.5 SDK Log Viewer

Inside the editor plugin, show recent SDK logs:

- Connection
- Auth
- Room events
- Lobby events
- Matchmaking events
- State sync
- Errors

### 24.6 Dashboard Deep Links

Add buttons:

- Open project dashboard
- Open session debugger
- Open docs
- Open examples

## Acceptance Criteria

- Plugin can be enabled in Godot
- Developer can configure project credentials inside Godot
- Developer can test connection from the editor
- Developer can generate at least one working example scene
- Developer can launch multiple local clients or receive clear instructions to do so
- Plugin links back to dashboard and docs

---

# Milestone 25 — Documentation & Tutorials v1

## Goal

Create documentation that makes developers successful without needing support.

## Why This Matters

Docs are part of the product.

The docs should reduce confusion, increase activation, and make the product feel trustworthy.

## Documentation Structure

Recommended docs structure:

```text
Getting Started
  - Create an account
  - Create a project
  - Choose game type
  - Install the Godot SDK
  - Connect your game
  - Authenticate a player
  - Join a room
  - Send your first event

Core Concepts
  - Project
  - Player
  - Session
  - Room
  - Lobby
  - Matchmaking Queue
  - Event Relay
  - Room State
  - Entity State
  - Persistence
  - Debugging

Guides
  - Build a lobby
  - Add ready checks
  - Add matchmaking
  - Build a chat room
  - Sync player state
  - Save player profile
  - Debug disconnects
  - Run multiple local clients

SDK Reference
  - Installation
  - Configuration
  - Signals
  - Methods
  - Error codes
  - Reconnection
  - Debug reports

Dashboard
  - Project setup
  - API keys
  - Rooms
  - Lobbies
  - Matchmaking
  - Persistence
  - Debugger

Limits & Pricing
  - CCU
  - Events
  - State updates
  - Storage
  - Bandwidth
  - Free tier limits
```

## Required Tutorials

### 25.1 Tutorial: Online in 10 Minutes

Goal:

- Create project
- Install SDK
- Connect
- Login guest
- Send/receive event

### 25.2 Tutorial: Build a Lobby with Ready Check

Goal:

- Create lobby
- Join lobby
- Display players
- Toggle ready
- Start game

### 25.3 Tutorial: Add Matchmaking

Goal:

- Enter queue
- Match two players
- Join room
- Start match

### 25.4 Tutorial: Simple State Sync

Goal:

- Create entity
- Patch state
- Receive changes
- Handle late join snapshot

### 25.5 Tutorial: Save Player Profile

Goal:

- Save display name
- Save XP
- Load profile

### 25.6 Tutorial: Debug a Failed Connection

Goal:

- Turn on SDK debug mode
- Read SDK logs
- Copy debug bundle
- Use dashboard session timeline

## Decision Guides

Add guides that help users choose the correct networking model.

Example:

```text
I am building a turn-based game
→ Use room state + persistence

I am building a casual co-op game
→ Use rooms + entity state + events

I am building a top-down arena
→ Use matchmaking + rooms + entity state + interpolation

I am building a competitive shooter
→ Use dedicated authoritative servers; default relay/state sync is not enough

I am building an MMO
→ Not supported yet
```

## Acceptance Criteria

- Docs have a complete getting started path
- Docs include SDK install and first connection
- Docs include at least 5 tutorials
- Docs document error codes
- Docs explain persistence vs state sync
- Docs explain what the product is not suitable for
- Dashboard and SDK link to relevant docs

---

# Milestone 26 — Simple Indie Pricing & Usage Metering

## Goal

Add usage tracking and simple pricing tiers suitable for indie Godot developers.

## Why This Matters

Developers need predictable pricing.

The product should avoid confusing billing at first.

## Deliverables

### 26.1 Usage Metering

Track usage per project and organization:

```text
Concurrent connections
Monthly active players
Monthly events
Monthly state updates
Persistence reads
Persistence writes
Storage used
Bandwidth estimate
Rooms created
Lobbies created
Matchmaking tickets
Errors
Rate-limit events
```

### 26.2 Dashboard Usage Page

Show:

- Current plan
- Current billing period
- Usage bars
- Limits
- Warnings when near limits
- Upgrade prompt

### 26.3 Pricing Tiers

Suggested simple tiers:

```text
Free — $0/month
- 1 project
- 20 CCU
- 50k events/month
- 100k state updates/month
- 100 MB persistence
- Community support

Indie — $15/month
- 3 projects
- 100 CCU
- 1M events/month
- 2M state updates/month
- 1 GB persistence
- Email support

Studio — $59/month
- 10 projects
- 500 CCU
- 10M events/month
- 20M state updates/month
- 10 GB persistence
- Priority support
- Advanced debugger retention

Growth — $199/month
- 25 projects
- 2,000 CCU
- 50M events/month
- 100M state updates/month
- 50 GB persistence
- Priority support
- Higher rate limits
```

### 26.4 Limit Enforcement

Implement soft and hard limits.

Soft limit:

- Warn developer
- Show dashboard alert
- Send email if configured

Hard limit:

- Reject excessive events/state updates
- Prevent new connections if CCU exceeded
- Reject persistence writes if storage exceeded

### 26.5 Billing Not Required Immediately

For alpha/beta, actual payment processing can come later.

First implement:

- Plans
- Limits
- Usage tracking
- Admin ability to assign plan manually

## Acceptance Criteria

- Usage is tracked per project
- Dashboard shows current usage and plan limits
- Plans exist in data model
- Limits can be enforced
- Free/Indie/Studio/Growth plans are represented
- Billing integration can be added later

---

# Milestone 27 — Landing Page & Conversion Flow

## Goal

Turn the starting landing page into a clear product page that converts Godot developers.

## Why This Matters

The landing page should immediately explain the value.

## Page Sections

### 27.1 Hero

Example copy:

```text
Godot multiplayer without building backend infrastructure.

Add rooms, lobbies, matchmaking, persistence, and simple state sync to your Godot game with a Godot-native SDK.
```

CTA:

```text
Start free
View examples
```

### 27.2 Problem Section

Explain:

```text
Building multiplayer is hard:
- WebSocket infrastructure
- Auth
- Rooms
- Lobbies
- Matchmaking
- Reconnects
- Persistence
- Debugging
```

### 27.3 Solution Section

Show:

```text
Create project
Install Godot addon
Connect your game
Add lobby/matchmaking/state sync
Debug from dashboard
```

### 27.4 Feature Sections

Feature blocks:

- Godot-native SDK
- Rooms and lobbies
- Matchmaking
- Event relay
- Simple state sync
- Persistence
- Multiplayer debugger
- Example projects

### 27.5 Code Snippet

Show a short GDScript example:

```gdscript
await GodotNet.connect()
await GodotNet.login_guest()
await GodotNet.matchmake("duel_1v1")

GodotNet.entity_state_changed.connect(func(entity_id, patch, state):
    print("Entity changed:", entity_id, state)
)
```

### 27.6 Templates Section

Show example projects:

- 1v1 arena
- Lobby ready check
- Chat room
- Persistent profile
- State sync demo

### 27.7 Pricing Preview

Show simple pricing:

- Free
- Indie
- Studio
- Growth

### 27.8 Honest Fit Section

Explain what the product is and is not for.

Good for:

- Indie Godot games
- Lobbies
- Matchmaking
- Casual multiplayer
- Party games
- Turn-based games
- Lightweight co-op

Not yet for:

- Competitive shooters
- MMOs
- Full anti-cheat
- Server-authoritative physics

## Acceptance Criteria

- Landing page clearly targets Godot developers
- Landing page explains core value in the first screen
- Landing page includes code sample
- Landing page links to examples and docs
- Landing page includes pricing preview
- Landing page has clear CTA to create project

---

# Milestone 28 — Closed Beta Readiness

## Goal

Prepare the platform for a small group of real Godot developers.

## Why This Matters

The next validation step is not more features. It is watching real users build with the product.

## Deliverables

### 28.1 Beta User Flow

Create a complete beta flow:

```text
Landing page
Sign up
Create project
Choose game type
Install SDK
Run example
Open dashboard debugger
Build own prototype
Give feedback
```

### 28.2 Feedback Collection

Add:

- Dashboard feedback button
- SDK feedback link
- Docs feedback button
- Example project issue links

Feedback categories:

- Setup problem
- SDK confusion
- Missing feature
- Bug
- Pricing concern
- Documentation issue
- Dashboard issue

### 28.3 Beta Success Metrics

Track:

```text
Account created
Project created
SDK connected successfully
First auth success
First room joined
First event sent
First lobby created
First matchmaking ticket created
First persistence write
First state sync update
Example project opened
Debugger used
```

### 28.4 Activation Score

Define activation as:

```text
A project is activated when:
- SDK connects successfully
- A player authenticates
- A room/lobby/match is created
- At least one event or state update is sent
```

### 28.5 Support Process

Create support workflow:

- User submits debug bundle
- Dashboard links session timeline
- Error explorer suggests likely issue
- Developer can respond quickly

## Acceptance Criteria

- Beta user can complete onboarding without manual help
- Feedback can be submitted from dashboard/docs
- Product tracks activation metrics
- Debug bundles help diagnose user issues
- At least 3-5 external Godot developers can test the platform

---

# Recommended Build Order

Build in this order:

```text
1. Account System & Product Onboarding
2. Godot-Native SDK Polish
3. Full Working Example Projects
4. Multiplayer Debugger Dashboard
5. Simple State Sync v1
6. Godot Editor Plugin
7. Documentation & Tutorials v1
8. Usage Metering & Simple Indie Pricing
9. Landing Page & Conversion Flow
10. Closed Beta Readiness
```

Reasoning:

- Accounts/onboarding make the product usable
- SDK polish makes examples easier
- Examples reveal SDK problems
- Debugger makes alpha/beta support easier
- State sync adds meaningful product value
- Editor plugin deepens Godot differentiation
- Docs turn the product into a self-serve system
- Pricing/metering prepares for paid users
- Landing page improves conversion
- Closed beta validates everything

---

# Non-Goals for This Phase

Avoid adding these until the platform has stronger validation:

```text
Voice chat
Advanced anti-cheat
Rollback networking
Server-authoritative physics
Large MMO zones
Cloud scripting
Complex economy systems
Tournaments
Friends/social graph
Guilds/clans
Dedicated server orchestration
Marketplace integrations
```

These can come later, but they should not distract from the core wedge.

---

# Key Product Principles

## 1. Godot First

Every feature should feel native to Godot.

Prefer:

```text
Signals
Nodes
Autoloads
Inspector config
Editor plugin
GDScript examples
Godot scenes
```

Avoid feeling like a generic REST/WebSocket service with a thin Godot wrapper.

## 2. Fast Time to First Success

A new user should achieve this quickly:

```text
Create project
Install SDK
Run example
See two clients connected
Send multiplayer event
```

## 3. Debuggability Is a Feature

When multiplayer fails, the platform should explain why.

Prioritize:

```text
Session timelines
Rejected message reasons
Connection diagnostics
SDK debug reports
Dashboard inspectors
Common error explanations
```

## 4. Honest Scope

Clearly explain what the product supports and what it does not.

This builds trust with indie developers.

## 5. Simple Pricing

Avoid complex usage math early.

Use simple tiers based on:

```text
Projects
CCU
Events
State updates
Storage
Support
```

---

# Claude Implementation Prompt

Use this plan to improve the existing platform.

Start by inspecting the current repository and dashboard implementation.

Then implement the milestones incrementally.

For each milestone:

1. Identify existing relevant code
2. Reuse current architecture where possible
3. Add missing database models/migrations
4. Add backend APIs/events/protocol messages
5. Update dashboard UI
6. Update Godot SDK
7. Add tests
8. Add documentation
9. Ensure acceptance criteria are met

Do not overbuild enterprise features.

Prioritize a polished Godot indie developer experience.

The most important outcomes are:

```text
- A developer can sign up and create a project
- A developer can choose their game type and needed features
- A developer can install the Godot SDK easily
- A developer can run a complete example project
- A developer can debug multiplayer sessions from the dashboard
- A developer can use simple state sync and persistence
- A developer understands pricing and limits
```

