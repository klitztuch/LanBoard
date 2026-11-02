# Requirements Document

## Introduction

On pages built on `ServicePageBase`, a second user event (for example a double-click) can start database work while the first event's work is still awaiting `AppDbContext`. EF Core rejects the second operation and the exception goes unhandled, so the circuit is torn down. Tearing it down disposes the circuit scope while the first query is still reading, which breaks that Npgsql connection mid-read. This is the error signature of GitHub issue #45 (`A second operation was started…`, `ArgumentOutOfRangeException` in `NpgsqlDataReader`, `Exception while reading from stream`), reproduced on `main` by double-clicking "Set active" on `/admin/party` (30/30 runs). PR #47 fixed the live-update path with `LiveReload`. This feature applies the same guarantees to user-triggered work on the remaining pages.

## Glossary

- **Service_Page**: A component that inherits `ServicePageBase`. On `main`: `TournamentDetail`, `TournamentList`, `LocationForm`, `Settings`, `Profile`, and the admin pages `Locations`, `ProfileFields`, `Roles`, `PartyList`, `PartyProfiles`, `Users`, `PartyEdit`
- **Circuit_Scope**: The DI scope shared by all components of one interactive circuit (or one prerender request), which owns one App_Db_Context
- **App_Db_Context**: The scoped EF Core `AppDbContext` used by the repositories and application services
- **Page_Db_Work**: Any database work a Service_Page starts: its initial load, a mutation through `RunAsync`, or a reload after a mutation (including calls made outside `RunAsync`, such as the reload in `PartyList.SetActive`)
- **Overlapping_Event**: A user event on a Service_Page that arrives while earlier Page_Db_Work in the same Circuit_Scope has not completed
- **Concurrency_Error**: EF Core's `InvalidOperationException` "A second operation was started on this context instance before a previous operation completed"
- **Protocol_Error**: An Npgsql failure that shows a broken or desynchronised connection, such as `ArgumentOutOfRangeException` in `NpgsqlDataReader`, `Exception while reading from stream`, or `BindComplete while expecting ReadyForQueryMessage`

## Requirements

### Requirement 1

**User Story:** As an admin, I want a double-click on an action button to be harmless, so that my session doesn't crash and the server's connections stay healthy.

#### Acceptance Criteria

1. WHEN an Overlapping_Event triggers Page_Db_Work, THE Service_Page SHALL start that work only after the earlier Page_Db_Work in the same Circuit_Scope has completed
2. WHEN a user double-clicks "Set active" on `/admin/party`, THE Service_Page SHALL keep the circuit alive
3. WHEN a user double-clicks "Set active" on `/admin/party`, THE Web_App log SHALL contain no Concurrency_Error and no Protocol_Error
4. WHEN Page_Db_Work runs after waiting for earlier work, THE Service_Page SHALL produce the same result as if the events had arrived one after another

### Requirement 2

**User Story:** As a developer, I want the guarantee to come from the shared base class, so that each page doesn't need its own guard and new pages are covered by default.

#### Acceptance Criteria

1. THE Service_Page base SHALL serialise all Page_Db_Work of a Service_Page, including work started outside `RunAsync`
2. WHILE a parent Service_Page and a child Service_Page (for example `Locations` and `LocationForm`) share a Circuit_Scope, THE Service_Page base SHALL serialise Page_Db_Work across both components
3. WHERE a component serialises its database work through `LiveReload` (`SeatGrid`, `Attendees`, `Tv`), THE Web_App SHALL keep that work serialised with any Service_Page work in the same Circuit_Scope
4. THE Service_Page base SHALL NOT require pages to disable buttons for correctness; any busy-state UI SHALL be optional feedback only

### Requirement 3

**User Story:** As an operator, I want page database work to never outlive its scope, so that closing a page can't break a pooled connection.

#### Acceptance Criteria

1. WHEN a Service_Page is disposed, THE Service_Page SHALL wait for its in-flight Page_Db_Work to complete before disposal finishes
2. WHEN a Service_Page has been disposed, THE Service_Page SHALL NOT start new Page_Db_Work
3. IF in-flight Page_Db_Work fails after the Service_Page was disposed, THEN THE Service_Page SHALL log the failure at `Debug` level and SHALL NOT raise it to the circuit

### Requirement 4

**User Story:** As a user, I want an unexpected database failure to show an error instead of killing my session, so that I can keep using the page.

#### Acceptance Criteria

1. IF Page_Db_Work fails with a Concurrency_Error, THEN THE Service_Page SHALL keep the circuit alive
2. IF Page_Db_Work fails with a Concurrency_Error, THEN THE Service_Page SHALL log the failure at `Error` level with the page name
3. IF Page_Db_Work fails with a Concurrency_Error, THEN THE Service_Page SHALL show the generic inline error message in place of the page content it could not update

### Requirement 5

**User Story:** As a maintainer, I want automated checks for these rules, so that a new page or refactor can't bring the problem back.

#### Acceptance Criteria

1. THE test suite SHALL include a test that fails when two Page_Db_Work items of one Service_Page overlap
2. THE test suite SHALL include a test that fails when Page_Db_Work of a parent and a child Service_Page in one Circuit_Scope overlap
3. THE test suite SHALL include a test that fails when disposal of a Service_Page completes while its Page_Db_Work is still running
4. WHEN a scripted browser run double-clicks "Set active" on `/admin/party` at least 30 times against a running AppHost, THE Web_App SHALL terminate no circuit and log zero Concurrency_Errors and zero Protocol_Errors

## Out of Scope

- The live-update path (`LiveReload`, prerender guards), already fixed by PR #47
- `NotificationHost`, which owns its own scope; its remaining races need two preference changes within one query's duration and are tracked separately if wanted
- Switching repositories to `IDbContextFactory<AppDbContext>`
