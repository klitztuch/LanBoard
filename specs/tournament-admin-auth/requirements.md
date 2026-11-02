# Requirements Document

## Introduction

Tournament mutations (adding/removing participants, starting a tournament, setting match winners) are currently gated only by UI rendering in `TournamentDetail.razor`. In Blazor Server, hidden or disabled elements are not a security boundary, so any authenticated user can invoke these operations by tampering with the DOM or replaying SignalR events. This feature moves the admin check server-side so that only admins can mutate tournament state, regardless of how the call is triggered (GitHub issue #18, follow-up to PR #16).

## Glossary

- **Tournament_Service**: The application-layer `TournamentService` that performs tournament reads and mutations
- **Tournament_Detail_Page**: The `TournamentDetail.razor` Blazor Server component that renders a tournament and calls Tournament_Service
- **Admin**: An authenticated user whose principal carries the `lanboard:isadmin` claim (the same identity the `"Admin"` authorization policy accepts)
- **Non_Admin**: Any user, authenticated or not, who is not an Admin
- **Mutating_Operation**: One of `AddParticipantAsync`, `RemoveParticipantAsync`, `StartAsync`, `SetMatchWinnerAsync` on Tournament_Service
- **Read_Operation**: Any Tournament_Service call that only returns tournament data without changing it
- **Authorization_Failure**: The outcome when a Non_Admin invokes a Mutating_Operation; the call is rejected and no state changes

## Requirements

### Requirement 1

**User Story:** As a LAN organizer, I want tournament mutations to be authorized on the server, so that participants cannot manipulate brackets by bypassing the UI.

#### Acceptance Criteria

1. WHEN a Non_Admin invokes a Mutating_Operation, THE Tournament_Service SHALL reject the call with an Authorization_Failure
2. WHEN a Non_Admin invokes a Mutating_Operation, THE Tournament_Service SHALL leave the tournament, its participants and its matches unchanged
3. WHEN an Admin invokes a Mutating_Operation, THE Tournament_Service SHALL perform the operation with the same behavior as before this change
4. THE Tournament_Service SHALL evaluate the admin check before any database write for every Mutating_Operation

### Requirement 2

**User Story:** As a developer, I want the admin check to use the same definition of "admin" as the rest of the app, so that authorization rules don't drift between features.

#### Acceptance Criteria

1. THE Tournament_Service SHALL treat a user as Admin using the same criterion as the existing `"Admin"` authorization policy (`lanboard:isadmin` claim)
2. THE Tournament_Service SHALL determine the caller's identity from the server-side authentication state, not from any value supplied by the client
3. IF the caller is unauthenticated, THEN THE Tournament_Service SHALL treat the caller as a Non_Admin

### Requirement 3

**User Story:** As a tournament participant, I want to keep viewing tournaments and brackets, so that the fix does not reduce what regular users can see.

#### Acceptance Criteria

1. WHEN a Non_Admin invokes a Read_Operation, THE Tournament_Service SHALL return the requested data as before this change
2. WHILE the current user is a Non_Admin, THE Tournament_Detail_Page SHALL continue to hide the admin controls for Mutating_Operations

### Requirement 4

**User Story:** As a user who somehow triggers a forbidden action, I want a clear outcome instead of a crash, so that the page stays usable.

#### Acceptance Criteria

1. IF a Mutating_Operation from the Tournament_Detail_Page fails with an Authorization_Failure, THEN THE Tournament_Detail_Page SHALL keep the circuit alive and keep rendering the tournament
2. IF a Mutating_Operation from the Tournament_Detail_Page fails with an Authorization_Failure, THEN THE Tournament_Detail_Page SHALL display a "not authorized" message to the user

### Requirement 5

**User Story:** As a maintainer, I want automated tests for the authorization rules, so that a future refactor can't silently remove them.

#### Acceptance Criteria

1. THE test suite SHALL include, for each Mutating_Operation, a test showing that a Non_Admin call results in an Authorization_Failure
2. THE test suite SHALL include, for each Mutating_Operation, a test showing that a Non_Admin call leaves persisted state unchanged
3. THE test suite SHALL include at least one test showing that an Admin call to a Mutating_Operation succeeds

## Open Questions

- Should tournament **creation/deletion** (if those exist outside the four methods named in the issue) be covered by the same check? Assumed **yes** for any other mutating method on Tournament_Service — to be confirmed during design.
- Should rejected attempts be written to the admin audit log (from #17)? Assumed **no** for this fix.
