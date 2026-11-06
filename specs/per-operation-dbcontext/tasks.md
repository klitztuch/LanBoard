# Implementation Plan

- [x] 1. Base branch
  - `git fetch`, create `fix/per-operation-dbcontext` from `origin/develop`
  - Build + `dotnet test` green before changing anything
  - _Requirements: —_

- [x] 2. `PerCall<TService>` (test-first)
- [x] 2.1 Unit tests `LanBoard.Web.Tests/Services/PerCallTests.cs`
  - Real `ServiceCollection` with a scoped fake service (`IFakeService`) that counts created and disposed instances (`IAsyncDisposable`)
  - Two sequential `Run` calls → two distinct instances, both disposed
  - `Run<TResult>` returns the call's result; scope disposed afterwards
  - Call throws → exception propagates, scope still disposed
  - Two overlapping calls (held by `TaskCompletionSource`) → distinct instances; neither disposed before its own call completes
  - _Requirements: 1.1, 1.2, 1.4, 7.1_

- [x] 2.2 Implement `LanBoard.Web/Services/PerCall.cs` and register it
  - `PerCall<TService>(IServiceScopeFactory)` with `Run<TResult>(Func<TService, Task<TResult>>)` and `Run(Func<TService, Task>)`, `await using var scope = scopeFactory.CreateAsyncScope()`
  - `Program.cs`: `builder.Services.AddTransient(typeof(PerCall<>));`
  - Make 2.1 green
  - _Requirements: 1.1, 1.2, 1.4, 5.1_

- [x] 3. `ServicePageBase`: event ordering, busy state, error containment (test-first)
- [x] 3.1 Unit tests `LanBoard.Web.Tests/Components/ServicePageBaseTests.cs`
  - bUnit test component deriving from `ServicePageBase` with buttons whose handlers await `TaskCompletionSource`s, renders `IsBusy` and `Error`; substitute `AuthenticationStateProvider`, real localizer or `IStringLocalizer` substitute, capturing logger
  - Two clicks → second handler starts only after the first completes
  - `IsBusy` true while a handler awaits, also while a click is queued; false after success and after failure
  - Synchronous handler and slow `OnInitializedAsync` never set `IsBusy`
  - Unexpected exception in a handler and in `OnInitializedAsync` → `Error == "Error.Generic"`, logged at `Error`, renderer has no unhandled exception
  - `BusinessRuleException` in `RunAsync` → unchanged domain message
  - Dispose while a click is queued → queued handler never runs; exception thrown after dispose → logged at `Debug` only
  - Child component whose handler awaits a parent `EventCallback` (both `ServicePageBase`) completes without deadlock
  - _Requirements: 2.1, 3.2, 3.3, 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 6.4, 7.2, 7.3_

- [x] 3.2 Implement in `LanBoard.Web/Components/ServicePageBase.cs`
  - `IHandleEvent` re-implementation with per-instance `SemaphoreSlim(1, 1)`, `IsBusy`, `_disposed` check after acquiring
  - `SetParametersAsync` override: `parameters.SetParameterProperties(this)`, then `base.SetParametersAsync(ParameterView.Empty)` in try/catch
  - Private `Contain(Exception)`: `LogDebug` when disposed, else `LogError` + `Error = L["Error.Generic"]` + `StateHasChanged()`
  - `IAsyncDisposable`: `DisposeAsync` sets `_disposed`, calls `protected virtual DisposeAsyncCore()`
  - XML doc: same-component `EventCallback` re-entrancy deadlocks
  - Make 3.1 green
  - _Requirements: 2.1, 3.2, 3.3, 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 6.4_

- [x] 3.3 Adapt `SeatGrid` and `Attendees` to the base disposal
  - Drop `@implements IAsyncDisposable` and their `DisposeAsync`; `protected override ValueTask DisposeAsyncCore() => _live?.DisposeAsync() ?? ValueTask.CompletedTask;`
  - _Requirements: 3.2_

- [x] 4. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`

- [x] 5. Architecture test `LanBoard.Web.Tests/Architecture/ComponentInjectionTests.cs`
  - Build a `ServiceCollection` via `AddInfrastructure` on a `HostApplicationBuilder` (dummy connection string, Steam options) and collect the service types registered with an Infrastructure implementation that are interfaces from `LanBoard.Application` = DB_Services
  - Reflect over all `IComponent` types in `LanBoard.Web`; fail with a list of `Component.Property : Type` for every `[Inject]` property whose type is a DB_Service
  - Sanity assertion: the DB_Service set contains `IAdminService` (guards against an empty set making the test vacuous)
  - Expected red until task 6 is done
  - _Requirements: 1.3, 5.2_

- [x] 6. Migrate components to `PerCall<T>`
- [x] 6.1 Admin pages
  - `PartyList`, `PartyEdit`, `Locations`, `LocationForm`, `Roles`, `Users`, `ProfileFields`, `PartyProfiles`, `AuditLog`
  - `@inject IXxxService X` → `@inject PerCall<IXxxService> X`; `X.Foo(a)` → `X.Run(s => s.Foo(a))`
  - _Requirements: 1.1, 1.3, 1.4, 1.5, 5.1_

- [x] 6.2 User pages
  - `TournamentList`, `TournamentDetail`, `Profile`, `Settings`, `SeatGrid`, `Attendees`
  - `Settings`: remove `_saving` and its guard, use `IsBusy` (`disabled="@IsBusy"` on the submit)
  - `SeatGrid`: keep `_live.RunAsync` around handler work (ordering against reloads), remove the now-unneeded guard comment if any
  - _Requirements: 1.1, 1.3, 1.4, 1.5, 2.4, 5.1, 5.3_

- [x] 6.3 Read-only `ComponentBase` pages
  - `Home`, `Tv`, `Stats`, `PartyArchive`, `PartyInfo`
  - _Requirements: 1.1, 1.3, 5.1_

- [x] 6.4 Second-delete behaviour
  - Check `AdminService.DeletePartyAsync`, `RoleService.DeleteAsync`, `LocationService.DeleteAsync`, `ProfileFieldService.DeleteAsync` for a missing id: each must throw the regular not-found `BusinessRuleException` (or be a no-op), not a `NullReferenceException` / `DbUpdateConcurrencyException`
  - Where it doesn't: map to the existing not-found error code, plus a test in `LanBoard.Infrastructure.Tests`
  - _Requirements: 2.2, 2.3_

- [x] 7. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`; the architecture test from 5 is now green

- [x] 8. Busy state in the UI
- [x] 8.1 Disabled style in `LanBoard.Web/Styles/app.css`
  - `button:disabled, input[type=submit]:disabled { @apply opacity-50 cursor-not-allowed; }`
  - _Requirements: 6.3_

- [x] 8.2 `disabled="@IsBusy"` on Action_Controls
  - `PartyList`, `PartyEdit`, `Locations`, `LocationForm`, `Roles`, `Users`, `ProfileFields`, `TournamentList`, `TournamentDetail`, `SeatGrid` (`Settings` done in 6.2)
  - Combine with existing conditions, e.g. `disabled="@(IsBusy || !_canManage)"`; leave purely client-side toggles (confirm/cancel) enabled
  - _Requirements: 6.1, 6.2, 6.5_

- [x] 9. Docs in code
  - `LiveReload` XML doc: "orders a component's loads and live reloads" instead of protecting the circuit `DbContext`
  - `CLAUDE.md` › Blazor Data Loading: replace the scoped-`DbContext` rule with "components use `PerCall<T>`; never inject DB_Services directly (enforced by `ComponentInjectionTests`)"
  - _Requirements: 5.1_

- [x] 10. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`

- [x] 11. Double-click smoke check `scripts/double-click-smoke/`
  - `package.json` (Playwright dev dependency), `smoke.mjs`, `run.sh` wrapper reading `aspire logs web` like `prerender-abort-smoke.sh`
  - Dev-login as Alice, ensure ≥ 2 parties, open `/admin/party`; 30× dispatch two `click` events on "Set active" in the same tick via `page.evaluate`; after each run assert no `#blazor-error-ui` / reconnect banner
  - Count `second operation`, `ArgumentOutOfRangeException`, `Exception while reading from stream` in new log lines; non-zero exit on any hit or lost circuit
  - _Requirements: 7.4, 7.5_

- [x] 12. Final Checkpoint - Ensure all tests pass
  - `dotnet build` + `dotnet test`
  - Start the AppHost; run `scripts/double-click-smoke/run.sh` and `scripts/prerender-abort-smoke.sh`, both green
  - Review against requirements 1–7 and the design; `git diff origin/develop -- LanBoard.Application LanBoard.Infrastructure` touches no service signatures or registrations (6.4 fixes excepted)
  - _Requirements: 3.1, 3.4, 5.4, 7.4, 7.5, 7.6_
