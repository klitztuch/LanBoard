# Requirements Document

## Introduction

All components of a Blazor Server circuit currently share one circuit-scoped `AppDbContext`, which supports only one operation at a time. Issue #45 shows the consequence: a double-click on "Set active" in `/admin/party` starts a second query on the same context, the unhandled `InvalidOperationException` crashes the circuit, and the scope disposes the context mid-read, which breaks a pooled connection. The same long-lived context is behind #44 (prerender abort) and the stale-tracking problem that today forces `AsNoTracking` everywhere. This fix follows Microsoft's guidance for Blazor Server and EF Core: every page operation gets its own short-lived DI scope with its own `AppDbContext`. In addition, user events are ordered per component, a visible busy state is shown, and the circuit survives unexpected errors.

## Glossary

- **Web_App**: The `LanBoard.Web` Blazor Server application including its EF Core / Npgsql data access
- **Circuit_Scope**: The DI scope of one interactive circuit (or one prerender request)
- **Operation_Scope**: A short-lived DI scope created for one Page_Operation, with its own `AppDbContext`, and disposed when the operation completes
- **DB_Service**: An Application service interface whose implementation uses `AppDbContext` (e.g. `IAdminService`, `ISeatService`, `IUserService`)
- **Data_Component**: A component that calls DB_Services (all `ServicePageBase` pages plus `Home`, `Tv`, `Stats`, `PartyArchive`, `PartyInfo`)
- **Service_Page**: A Data_Component deriving from `ServicePageBase`
- **Page_Operation**: One call from a Data_Component to a DB_Service, from a lifecycle method, an event handler or a Live_Reload
- **Live_Reload**: Notifier-driven reload work scheduled via `LiveReload`
- **Overlapping_Event**: A user event (click, submit, input change) of a Service_Page that arrives while a previous user event of the same Service_Page is still being handled, e.g. a double-click
- **Action_Control**: A button or submit control of a Service_Page whose handler starts a Page_Operation
- **Circuit_Crash**: Teardown of the circuit caused by an unhandled exception, shown to the user as the Blazor error UI / reconnect banner
- **Corrupted_Connection**: A pooled Npgsql connection whose protocol state is out of sync with the server

## Requirements

### Requirement 1: No shared DbContext in a circuit

**User Story:** As a user, I want simultaneous actions and live updates on my page to never interfere with each other, so that the page doesn't crash and the database stays healthy.

#### Acceptance Criteria

1. WHEN a Data_Component starts a Page_Operation, THE Web_App SHALL run it in a new Operation_Scope.
2. WHEN a Page_Operation completes, successfully or with an error, THE Web_App SHALL dispose its Operation_Scope.
3. THE Web_App SHALL NOT resolve `AppDbContext` or a DB_Service from a Circuit_Scope within an interactive circuit.
4. WHEN two Page_Operations of one circuit overlap (including Live_Reloads and nested components such as `LocationForm`), THE Web_App SHALL run them on different `AppDbContext` instances.
5. WHEN a Page_Operation reads data, THE Web_App SHALL return the current database state, unaffected by entities tracked in earlier Page_Operations of the same circuit.

### Requirement 2: Overlapping events behave predictably

**User Story:** As a user, I want repeated clicks to give a sensible result, so that I'm not confused by errors that only exist because I clicked twice.

#### Acceptance Criteria

1. WHEN an Overlapping_Event arrives, THE Service_Page SHALL start its handler only after the previous handler of that Service_Page has completed.
2. WHEN an Overlapping_Event repeats an idempotent action (e.g. "Set active" on the already active party), THE Web_App SHALL complete both operations without an error message.
3. IF an Overlapping_Event repeats an action whose target no longer exists (e.g. a second "Delete"), THEN THE Web_App SHALL show the same user-facing error message as for a non-overlapping request on a missing target.
4. WHEN Live_Reloads of one Data_Component overlap with each other or with that component's handlers, THE Data_Component SHALL apply their results in the order the operations were started.

### Requirement 3: Disposal never interrupts a query

**User Story:** As the developer, I want circuit teardown to never interrupt a running query, so that no Corrupted_Connection is returned to the pool.

#### Acceptance Criteria

1. WHEN a Circuit_Scope is disposed while a Page_Operation is in flight, THE Web_App SHALL let the Page_Operation finish on its Operation_Scope.
2. WHEN a Service_Page is disposed, THE Web_App SHALL NOT start handlers of that Service_Page that were queued but not yet started.
3. WHEN a Page_Operation completes after its Data_Component was disposed, THE Web_App SHALL NOT log it at `Error` level.
4. THE Web_App SHALL NOT return a Corrupted_Connection to the connection pool when a circuit or prerender request ends during a Page_Operation.

### Requirement 4: Circuit survives failing page operations

**User Story:** As a user, I want a failed action to show an error instead of killing the page, so that I can keep working without reloading.

#### Acceptance Criteria

1. IF a Page_Operation of a Service_Page throws an unexpected exception, THEN THE Web_App SHALL keep the circuit alive (no Circuit_Crash).
2. IF a Page_Operation of a Service_Page throws an unexpected exception, THEN THE Service_Page SHALL show the generic, localised error message.
3. IF a Page_Operation throws an unexpected exception, THEN THE Web_App SHALL log it at `Error` level.
4. THE Web_App SHALL keep the existing handling of expected domain errors (permission denied, validation, business rules) unchanged.

### Requirement 5: Uniform and enforced coverage

**User Story:** As the developer, I want every component to get the same mechanism, so that new pages don't reintroduce a shared context.

#### Acceptance Criteria

1. THE Web_App SHALL provide Operation_Scopes to every Data_Component through one shared mechanism.
2. THE Web_App SHALL have an automated test that fails IF a component injects a DB_Service directly.
3. THE Web_App SHALL replace the ad-hoc overlap guards in `Settings` and `SeatGrid` with the shared mechanism, or document why they remain.
4. THE Web_App SHALL keep the signatures and DI registrations of Application services and Infrastructure repositories unchanged.

### Requirement 6: Visible busy state

**User Story:** As a user, I want buttons to be disabled while my action is running, so that I can see that something is happening and don't click twice.

#### Acceptance Criteria

1. WHILE a Page_Operation started by a user event is in flight, THE Service_Page SHALL disable its Action_Controls.
2. WHEN that Page_Operation completes, successfully or with an error, THE Service_Page SHALL re-enable its Action_Controls.
3. WHILE an Action_Control is disabled, THE Service_Page SHALL render it with a visually distinct disabled style (reduced opacity, `cursor-not-allowed`).
4. WHILE only a Live_Reload or the initial load is in flight, THE Service_Page SHALL keep its Action_Controls enabled.
5. THE Web_App SHALL rely on Requirements 1 and 2, not on the busy state, for correctness, because a same-tick double-click reaches the server before the disabled state is rendered.

### Requirement 7: Regression protection

**User Story:** As the developer, I want the double-click scenario covered by automated checks, so that the bug doesn't silently come back.

#### Acceptance Criteria

1. THE Web_App SHALL have unit tests verifying that each Page_Operation gets its own Operation_Scope and that the scope is disposed on success and on failure.
2. THE Web_App SHALL have unit tests verifying that Overlapping_Events are handled in order, that queued handlers are skipped after disposal, and that unexpected exceptions don't escape.
3. THE Web_App SHALL have a unit test verifying that the busy state is set while a user-event Page_Operation runs and cleared after it completes or fails.
4. WHEN a scripted check double-clicks "Set active" on `/admin/party` 30 times, THE Web_App SHALL log zero `second operation`, `ArgumentOutOfRangeException` (Npgsql) and `Exception while reading from stream` errors.
5. WHEN a scripted check double-clicks "Set active" on `/admin/party` 30 times, THE Web_App SHALL keep the circuit alive in all 30 runs.
6. WHEN `scripts/prerender-abort-smoke.sh` runs after the change, THE script SHALL pass.
