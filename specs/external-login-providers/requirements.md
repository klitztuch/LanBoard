# Requirements Document

## Introduction

LanBoard currently supports Steam as its only login method, and Steam is hard-wired into authentication, the user model (`SteamName`), the user service and several pages. This feature makes external login providers first-class and interchangeable: Discord is added as a second provider equal to Steam, and the structure is built so that further providers (e.g. Twitch, Battle.net) can be added later with only provider-specific code. Because the same person may sign in through different providers, users can link several providers to one LanBoard account instead of ending up with duplicate accounts.

## Glossary

- **Auth_System**: The LanBoard web authentication layer (login/link/unlink endpoints, external auth handlers, cookie sign-in)
- **External_Provider**: A third-party login service registered with LanBoard (initially Steam and Discord)
- **Provider_Registry**: The central list of External_Providers, each with a key, display name, icon and optional profile-link format
- **Enabled_Provider**: An External_Provider that is registered and has its required configuration (e.g. API key, client ID/secret) present
- **Linked_Identity**: A `UserIdentity` connecting one External_Provider account (provider key + provider user ID) to a LanBoard User
- **Primary_Identity**: The Linked_Identity whose provider name and avatar the User shows when no nickname is set
- **Provider_Name**: The account name the External_Provider reports for a Linked_Identity (e.g. Steam persona name, Discord global name)
- **Effective_Name**: The name LanBoard displays for a User: the nickname if set, otherwise the Primary_Identity's Provider_Name
- **Connected_Accounts_Section**: The part of the Settings page that lists, links and unlinks a User's Linked_Identities
- **Provider_Feature**: Functionality that only works with a specific External_Provider (e.g. Steam game status, Steam profile link)

## Requirements

### Requirement 1

**User Story:** As a LAN participant, I want to sign in with either Steam or Discord, so that I can use the account I actually have and prefer.

#### Acceptance Criteria

1. THE Auth_System SHALL offer one login option per Enabled_Provider on the home page
2. THE Auth_System SHALL offer one login option per Enabled_Provider in the navigation menu for unauthenticated users
3. WHEN a user completes a login with an External_Provider whose account is not yet a Linked_Identity, THE Auth_System SHALL create a new User with that account as its Primary_Identity
4. WHEN a user completes a login with an External_Provider whose account is already a Linked_Identity, THE Auth_System SHALL sign the user in as the User owning that Linked_Identity
5. WHEN a login succeeds, THE Auth_System SHALL redirect the user to the requested return URL if it is a local URL
6. IF the requested return URL is missing or not a local URL, THEN THE Auth_System SHALL redirect the user to `/`

### Requirement 2

**User Story:** As a developer, I want login providers to be defined in one central place, so that adding another provider later does not require changes to user, session or page code.

#### Acceptance Criteria

1. THE Provider_Registry SHALL be the single source for the list of External_Providers shown in login buttons, the Connected_Accounts_Section and profile badges
2. THE Auth_System SHALL handle login, linking and user creation through one provider-independent flow that receives provider key, provider user ID, Provider_Name and avatar URL
3. WHERE an External_Provider's required configuration is missing, THE Auth_System SHALL treat that provider as disabled and hide its login and link options
4. IF a login or link request names a provider key that is not an Enabled_Provider, THEN THE Auth_System SHALL reject the request with HTTP 404
5. THE Auth_System SHALL keep the existing development login (`/auth/dev-login`) working unchanged

### Requirement 3

**User Story:** As a user, I want my displayed name and avatar to come from my chosen login account, so that my profile looks right no matter which provider I use.

#### Acceptance Criteria

1. THE Auth_System SHALL store the Provider_Name and avatar URL per Linked_Identity
2. WHEN a user logs in through a Linked_Identity, THE Auth_System SHALL update that Linked_Identity's stored Provider_Name and avatar URL from the External_Provider
3. THE Auth_System SHALL display the Effective_Name as the User's name
4. THE Auth_System SHALL display the Primary_Identity's avatar as the User's avatar
5. WHEN a user selects a different Linked_Identity as Primary_Identity in the Connected_Accounts_Section, THE Auth_System SHALL use that identity's Provider_Name and avatar from then on
6. WHEN the schema migration runs, THE Auth_System SHALL keep every existing Steam user's account, Steam identity, name, nickname and avatar unchanged, with the Steam identity as Primary_Identity

### Requirement 4

**User Story:** As a user, I want to connect additional login providers to my existing account, so that I end up with one LanBoard account regardless of how I sign in.

#### Acceptance Criteria

1. WHILE a user is signed in, THE Connected_Accounts_Section SHALL show every Enabled_Provider with its link status for that User
2. WHEN a signed-in user completes a link flow with an External_Provider account that is not a Linked_Identity, THE Auth_System SHALL add that account as a Linked_Identity to the signed-in User
3. IF the External_Provider account in a link flow is already a Linked_Identity of a different User, THEN THE Auth_System SHALL reject the link and show an error message in the Connected_Accounts_Section
4. IF the External_Provider account in a link flow is already a Linked_Identity of the signed-in User, THEN THE Auth_System SHALL leave the User unchanged
5. WHEN a link flow completes, THE Auth_System SHALL keep the user signed in as the same User
6. THE Auth_System SHALL allow at most one Linked_Identity per External_Provider per User

### Requirement 5

**User Story:** As a user, I want to disconnect a login provider from my account, so that I can stop using an account I no longer want associated with LanBoard.

#### Acceptance Criteria

1. WHEN a user unlinks a Linked_Identity and the User has at least one other Linked_Identity, THE Auth_System SHALL remove that Linked_Identity
2. IF a user attempts to unlink their only Linked_Identity, THEN THE Auth_System SHALL reject the request and leave the Linked_Identity in place
3. WHEN a user unlinks their Primary_Identity, THE Auth_System SHALL make the oldest remaining Linked_Identity the Primary_Identity
4. WHEN a Linked_Identity is unlinked, THE Auth_System SHALL keep the User's seats, RSVPs, tournament entries, roles, profile and nickname unchanged
5. THE Auth_System SHALL write an audit log entry for each link and unlink operation

### Requirement 6

**User Story:** As a user without a Steam account, I want LanBoard to work fully for me, so that I am not a second-class participant.

#### Acceptance Criteria

1. WHERE a User has no Linked_Identity for the provider a Provider_Feature requires, THE Auth_System SHALL hide that Provider_Feature for that User instead of showing an error
2. WHERE a User has a Steam Linked_Identity, THE Auth_System SHALL keep Steam game status and the Steam profile link working as before
3. THE Auth_System SHALL show a badge, and where the provider defines one a profile link, for each of a User's Linked_Identities on the attendee list and profile page
4. THE Auth_System SHALL apply roles, permissions and seat assignments to a User independently of which External_Provider was used to sign in

### Requirement 7

**User Story:** As an operator, I want failed or cancelled provider logins to be handled cleanly, so that users are not left on error pages or with broken sessions.

#### Acceptance Criteria

1. IF a user cancels or denies authorization at the External_Provider, THEN THE Auth_System SHALL redirect the user to the page they started from without signing them in
2. IF the External_Provider returns an error during login, THEN THE Auth_System SHALL show a localized error message and leave the user signed out
3. IF the External_Provider returns an error during a link flow, THEN THE Auth_System SHALL keep the user signed in as the same User and show a localized error message
4. THE Auth_System SHALL request only the minimum provider scopes needed to read the provider user ID, Provider_Name and avatar (for Discord: `identify`)
