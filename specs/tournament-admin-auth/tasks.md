# Implementation Plan

- [x] 1. Add the current-user abstraction and shared admin claim
  - Create `LanBoard.Application/Users/ICurrentUser.cs` with `Task<bool> IsAdminAsync(CancellationToken ct = default)`
  - Add `AdminClaim` (`Type = "lanboard:isadmin"`, `Value = "true"`) in `LanBoard.Web/Extensions/AuthExtensions.cs`
  - Replace the hard-coded claim strings in `AuthExtensions` (claim + `"Admin"` policy), `DevAuthExtensions`, `NavMenu.razor` and `TournamentDetail.razor` with `AdminClaim`
  - _Requirements: 2.1_

- [x] 2. Enforce admin authorization in TournamentService (test-first)
- [x] 2.1 Wire `ICurrentUser` into the existing tests
  - Add an `ICurrentUser` substitute to `TournamentServiceTests`, defaulting `IsAdminAsync` to `true`
  - Pass it to the `TournamentService` constructor (add the constructor parameter so the project compiles)
  - All existing tests must still pass unchanged — they are the "admin succeeds" proof
  - _Requirements: 1.3, 5.3_

- [x] 2.2 Write failing non-admin tests
  - For each of `CreateAsync`, `AddParticipantAsync`, `RemoveParticipantAsync`, `StartAsync`, `SetMatchWinnerAsync`:
    - `<Method>_NotAdmin_ThrowsUnauthorizedAccess`
    - `<Method>_NotAdmin_DoesNotTouchRepositories` (no calls received on any of the three repository substitutes)
  - `GetWithDetailsAsync_NotAdmin_ReturnsTournament` and `GetByPartyAsync_NotAdmin_ReturnsTournaments`
  - _Requirements: 1.1, 1.2, 1.4, 3.1, 5.1, 5.2_

- [x] 2.3 Implement `EnsureAdminAsync` in TournamentService
  - Private `EnsureAdminAsync(ct)` that throws `UnauthorizedAccessException("Only admins can modify tournaments.")` when `IsAdminAsync` returns `false`
  - Call it as the first statement of the five mutating methods; leave read methods untouched
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 3.1_

- [x] 3. Checkpoint - Verify all tests pass
  - Run `dotnet test`
  - Address any failures before continuing

- [x] 4. Provide the Blazor implementation of ICurrentUser
  - Create `LanBoard.Web/Services/CurrentUser.cs` using `AuthenticationStateProvider`; return `true` only when the principal is authenticated and has `AdminClaim`
  - Register `AddScoped<ICurrentUser, CurrentUser>()` in `AddAuth()`
  - _Requirements: 2.1, 2.2, 2.3_

- [x] 5. Handle authorization failures in the pages
- [x] 5.1 TournamentDetail.razor
  - Wrap the service calls in `ToggleParticipantAsync`, `StartAsync`, `SetWinnerAsync` in `try/catch (UnauthorizedAccessException)`, set `_error = "Keine Berechtigung für diese Aktion."`, then reload state
  - Render a dismissible red error banner when `_error` is set
  - Keep `_isAdmin` UI gating as is
  - _Requirements: 3.2, 4.1, 4.2_

- [x] 5.2 TournamentList.razor
  - Same `try/catch` + `_error` banner around `CreateAsync`
  - _Requirements: 4.1, 4.2_

- [x] 6. Final Checkpoint - Ensure all tests pass
  - Run `dotnet build` and `dotnet test`
  - Verify every requirement (1.1–5.3) is covered per the traceability table in `design.md`
  - Review implementation against design
