# Implementation Plan

- [ ] 1. Baseline
  - Work on branch `claude/clever-maxwell-6y73vy` (rebased on the latest `develop` if it moved)
  - Build and run `dotnet test` before changing anything; both must be green
  - _Requirements: —_

- [ ] 2. Domain model (test-first)
- [ ] 2.1 Unit tests `LanBoard.Infrastructure.Tests/Entities/UserTests.cs`
  - `AddIdentity` on a user without identities → identity is primary, `DisplayName`/`AvatarUrl` come from it
  - Second `AddIdentity` → not primary, display values unchanged; second identity of the same provider → `InvalidOperationException`
  - `SetPrimary` → `DisplayName`/`AvatarUrl` switch to the new primary; nickname still wins over Provider_Name
  - `SyncIdentity` on the primary updates the display values; on a non-primary it updates only the identity
  - `RemoveIdentity` of the primary → oldest remaining (min `CreatedAt`) becomes primary; removing the last identity → `InvalidOperationException`; unknown provider → `InvalidOperationException`
  - `SetNickname(null)` → `DisplayName` = primary Provider_Name
  - Normalization: blank Provider_Name falls back to `ProviderUserId`; names truncated to 100; a non-https or over-long avatar URL is stored as `null`
  - Replace the existing `SyncSteamName` tests
  - _Requirements: 3.1, 3.3, 3.4, 3.5, 4.6, 5.2, 5.3_

- [ ] 2.2 Implement the domain changes
  - `LanBoard.Application/Users/LoginProviders.cs` (`Steam`, `Discord`, `Dev`) and `ExternalLogin.cs` record
  - `UserIdentity`: add `ProviderName`, `AvatarUrl`, `IsPrimary`, `LastLoginAt`
  - `User`: `AddIdentity`, `SyncIdentity`, `RemoveIdentity`, `SetPrimary`, private `RefreshDisplay`; `SetNickname` refreshes from the primary
  - Remove `SteamName`/`SyncSteamName` and fix every reference (`UserService`, `DevAuthExtensions`, `ProfileService.GetSettingsAsync`, test builders). `ProfileSettings.SteamName` temporarily reads the primary identity's `ProviderName`
  - Make 2.1 green
  - _Requirements: 3.1, 3.3, 3.4, 3.5, 4.6, 5.2, 5.3_

- [ ] 3. Persistence
- [ ] 3.1 EF configuration and repository
  - `UserIdentityConfiguration`: `ProviderName` (max 100, required), `AvatarUrl` (max 500); unique index `(UserId, Provider)`; unique filtered index `(UserId)` with filter `"IsPrimary" = true`
  - `UserConfiguration`: drop the `SteamName` mapping
  - `IUserRepository`/`UserRepository`: `FindByProviderAsync` includes `Identities` (tracked); new `GetForUpdateWithIdentitiesAsync(Guid id)`
  - _Requirements: 3.1, 4.6_

- [ ] 3.2 Migration `ExternalLoginProviders`
  - Generate it with `dotnet ef migrations add`, then edit it: add the columns (`ProviderName` nullable at first), run the `UPDATE … FROM "Users"` data copy with `IsPrimary = true`, make `ProviderName` non-nullable, create the indexes, drop `Users.SteamName`
  - `Down`: re-add `SteamName` filled from the primary identity, drop the new columns and indexes
  - Verify against a local Postgres seeded with a Steam user and a Dev user: name, nickname, avatar and identity are unchanged after `Up`, and `Down` restores `SteamName`
  - _Requirements: 3.6_

- [ ] 4. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`

- [ ] 5. UserService (test-first)
- [ ] 5.1 Unit tests `LanBoard.Infrastructure.Tests/Services/UserServiceTests.cs`
  - `SignInExternalAsync`: unknown account → new user with a primary identity and save; known account → `SyncIdentity`, `LastLoginAt` set, same user returned; unique-violation on create → re-read and return the existing user
  - `LinkExternalAsync`: free account → identity added, saved, audit `IdentityLinked`; account owned by another user → `BusinessRuleException(Identity.LinkedToOtherUser)`, no save; owned by the same user → sync only, no audit; user already has another account of that provider → `Identity.ProviderAlreadyLinked`
  - `UnlinkAsync`: success → removed, saved, audit `IdentityUnlinked`; only identity → `Identity.LastCannotUnlink`; unknown provider → `Identity.NotFound`; seats/RSVPs/roles untouched (no other repositories called)
  - `SetPrimaryIdentityAsync`: switches the primary and saves; unknown provider → `Identity.NotFound`
  - Replace the `GetOrCreateAndSyncSteamProfileAsync` tests
  - _Requirements: 1.3, 1.4, 3.2, 3.5, 4.2, 4.3, 4.4, 4.6, 5.1, 5.2, 5.3, 5.4, 5.5_

- [ ] 5.2 Implement in `UserService` / `IUserService`
  - The four methods from the design; the constructor gains `IAdminAuditLogRepository`; remove `GetOrCreateAndSyncSteamProfileAsync`
  - `ErrorCodes`: `Identity.LinkedToOtherUser`, `Identity.ProviderAlreadyLinked`, `Identity.LastCannotUnlink`, `Identity.NotFound`, `Auth.ProviderError`, `Auth.LinkSessionMismatch`
  - `AuditActions`: `IdentityLinked`, `IdentityUnlinked`
  - en + de resx entries for the new error codes and audit actions (`ResourceParityTests` and `CatalogCoverageTests` must stay green)
  - The Steam `OnTicketReceived` in `AuthExtensions` temporarily calls `SignInExternalAsync`, so the app keeps compiling and working
  - Make 5.1 green
  - _Requirements: 1.3, 1.4, 3.2, 4.2, 4.3, 4.4, 4.6, 5.1, 5.2, 5.3, 5.5_

- [ ] 5.3 `LanBoardPrincipalFactory` and dev login
  - Unit tests: claims `Name` = Effective_Name, `NameIdentifier` = `{provider}:{id}`, `lanboard:userid`, `lanboard:avatarurl` (only if set), `lanboard:provider`
  - Implement `LanBoard.Web/Auth/LanBoardPrincipalFactory.cs`
  - `DevAuthExtensions` uses `SignInExternalAsync(new ExternalLogin("Dev", name, name, null))` and the factory; role assignment from `DevUserRoles` unchanged
  - Steam `OnTicketReceived` builds its principal with the factory
  - _Requirements: 2.5, 6.4_

- [ ] 6. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`

- [ ] 7. Provider abstraction
- [ ] 7.1 `IExternalLoginProvider` and `LoginProviderRegistry` (test-first)
  - Tests `LanBoard.Web.Tests/Auth/LoginProviderRegistryTests.cs` with fake providers: `Enabled` contains only configured ones, in registration order; `FindEnabled` is case-insensitive and returns `null` for disabled or unknown keys; `Find` returns disabled ones
  - Implement the interface and registry in `LanBoard.Web/Auth/`; `AddAuth` adds schemes only for configured providers and logs a warning outside Development when none are enabled
  - _Requirements: 2.1, 2.3_

- [ ] 7.2 `SteamLoginProvider`
  - Move the SteamID, persona-name and avatar logic out of `AuthExtensions` into `LanBoard.Web/Auth/Providers/SteamLoginProvider.cs`; profile URL `https://steamcommunity.com/profiles/{id}`
  - `SteamConfiguration.ApiKey`: remove `[Required]`; configured when non-empty
  - `SessionPollingService` skips polling when Steam is not configured
  - Tests: `IsConfigured` with and without a key; mapping from a principal with an OpenID `NameIdentifier` URL; Steam API returning no avatar → `AvatarUrl` null
  - _Requirements: 2.3, 6.1, 6.2_

- [ ] 7.3 `DiscordLoginProvider`
  - Add `AspNet.Security.OAuth.Discord` to `Directory.Packages.props` (latest 10.0.x matching the Steam package) and to `LanBoard.Web.csproj`
  - Implement `LanBoard.Web/Auth/Providers/DiscordLoginProvider.cs`: options from `Authentication:Discord`, scope `identify` only, map the `/users/@me` payload in `OnCreatingTicket` into the ticket items; profile URL `https://discord.com/users/{id}`; add `wwwroot/discord-logo.svg`
  - Tests for the pure mapping function: `global_name` → name, fallback to `username`, custom avatar URL with `?size=128`, default avatar index `(id >> 22) % 6`, blank names fall back to the ID; `IsConfigured` needs both ClientId and ClientSecret; the configured scopes are exactly `identify`
  - _Requirements: 1.1, 2.3, 7.4_

- [ ] 8. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`

- [ ] 9. Login and link flow
- [ ] 9.1 Tests `LanBoard.Web.Tests/Auth/ExternalLoginHandlerTests.cs`
  - Login mode: `ctx.Principal` replaced by the factory principal; `SignInExternalAsync` called with the mapped login; preferred culture applied
  - Mapping returns `null` → handled, redirect to `/login?error=Auth.ProviderError`, no sign-in
  - Link mode: `LinkExternalAsync` called for the cookie user; `HandleResponse` called; redirect to `/auth/refresh?returnUrl=/settings?linked=Discord`; no sign-in issued
  - Link mode with `BusinessRuleException` → redirect `/settings?linkError={code}`; cookie user ≠ `linkUser` → `Auth.LinkSessionMismatch`, no link call
  - Remote failure: cancel → redirect to the stored return URL; error in login mode → `/login?error=Auth.ProviderError&returnUrl=…`; error in link mode → `/settings?linkError=Auth.ProviderError`
  - _Requirements: 1.3, 1.4, 4.2, 4.3, 4.5, 7.1, 7.2, 7.3_

- [ ] 9.2 Implement `ExternalLoginHandler` and wire it to both providers
  - `OnTicketReceived` and `OnRemoteFailure` of the Steam and Discord schemes delegate to the handler; remove the remaining inline Steam logic from `AuthExtensions`
  - Make 9.1 green
  - _Requirements: 1.3, 1.4, 2.2, 4.2, 4.3, 4.5, 7.1, 7.2, 7.3_

- [ ] 9.3 Tests `LanBoard.Web.Tests/Extensions/AuthEndpointTests.cs`
  - `/auth/login/{provider}`: unknown or disabled → 404; valid → challenge on that scheme with `RedirectUri` = local `returnUrl`; non-local or missing `returnUrl` → `/`; `lanboard:returnUrl` item set
  - `/auth/link/{provider}`: unknown → 404; invalid antiforgery → 400; valid → challenge with `lanboard:mode=link` and `lanboard:linkUser` = current user ID
  - _Requirements: 1.5, 1.6, 2.4, 4.2_

- [ ] 9.4 Implement `MapAuthEndpoints`
  - Login and link endpoints in `AuthExtensions`; remove `/auth/login` and `/auth/logout` from `Program.cs` (logout moves unchanged)
  - Cookie: `LoginPath = "/login"`; `DefaultChallengeScheme` = cookie scheme
  - Make 9.3 green
  - _Requirements: 1.5, 1.6, 2.4, 4.2_

- [ ] 10. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`

- [ ] 11. UI
- [ ] 11.1 Provider-neutral read models
  - `LinkedAccountView(Provider, ProviderUserId, ProviderName, IsPrimary)`; `ProfileView.SteamProfileUrl` → `Accounts`; `ProfileSettings.SteamName` → `PrimaryProviderName` + `Accounts`
  - Update `ProfileServiceTests`: accounts listed for Steam-only, Discord-only and linked users; played games empty without Steam
  - _Requirements: 3.3, 6.1, 6.3_

- [ ] 11.2 `ProviderBadges.razor` (bUnit test-first)
  - Tests: one badge per known provider; link with `target="_blank" rel="noopener noreferrer"` only when `ProfileUrl` is non-null; `Dev` and unknown providers skipped; linked-but-disabled providers still shown
  - Implement; replace the Steam-only markup on `Attendees.razor` (remove `SteamUrl`) and `Profile.razor`
  - _Requirements: 2.1, 6.1, 6.2, 6.3_

- [ ] 11.3 `/login` page and `LoginButtons.razor`
  - bUnit tests: one button per enabled provider pointing to `/auth/login/{key}?returnUrl=…`; no buttons for disabled providers; the `error` query shows the localized `Auth.ProviderError` message
  - Implement the static SSR `/login` page and `LoginButtons`; use them in `Home.razor` and `NavMenu.razor`; replace `Nav.LoginWithSteam` with `Login.With` and add `Login.Title` (en + de)
  - _Requirements: 1.1, 1.2, 2.1, 7.2_

- [ ] 11.4 `ConnectedAccounts.razor` in Settings
  - bUnit tests: one row per enabled provider with linked/not-linked state and Provider_Name; "Connect" is a POST form to `/auth/link/{key}` with an antiforgery token; "Disconnect" disabled when only one identity is linked; primary marked and "Make primary" hidden on it; the `linkError` query shows the localized message; `linked` shows a success note
  - Implement: Disconnect and Make primary via `PerCall<IUserService>`, then force-navigate to `/auth/refresh?returnUrl=/settings`; business errors shown through the existing `ErrorBanner`
  - `Settings.razor`: add the section; nickname placeholder/help use `PrimaryProviderName`; add the new resx keys (en + de)
  - _Requirements: 3.5, 4.1, 4.3, 5.1, 5.2, 7.3_

- [ ] 12. Configuration
  - `appsettings.json`: empty `Authentication:Discord:ClientId`/`ClientSecret` placeholders; leave `Steam:ApiKey` to user secrets as before
  - Test (`LoginProviderRegistryTests` or startup): with only Discord configured, the app starts and Steam is disabled; with nothing configured in Development, only the dev login is available
  - _Requirements: 2.3, 2.5_

- [ ] 13. Checkpoint - Verify all tests pass
  - `dotnet build` + `dotnet test`
  - Grep for leftover Steam hard-wiring outside `SteamLoginProvider`, `SessionService` and `SteamApiClient` (`"Steam"` literals, `SteamName`, `steam-logo` outside `ProviderBadges`/`SteamLoginProvider`)
