# Microsoft sign-in (Azure app setup)

BL.Linux signs in with its own Azure app registration, **Application (client) ID
`e2e40a45-c8e9-47ef-bf59-de46d75e5f86`**. It is a public client: no secret or certificate is used or needed.
The ID is built into the launcher (`AccountManager.DefaultClientId`); Settings > Accounts can override it, and so
can `BL_MSA_CLIENT_ID`.

## Settings the app needs

In [portal.azure.com](https://portal.azure.com) > **App registrations** > the app:

1. **Manifest**: `"signInAudience": "PersonalMicrosoftAccount"` (shown as "Personal Microsoft accounts only") and,
   in the `api` section, `"requestedAccessTokenVersion": 2` (Azure refuses personal accounts without it). An app
   created as "Accounts in this organizational directory only" can be switched here; "Accounts in any organizational
   directory and personal Microsoft accounts" works too. The launcher uses the `consumers` endpoint.
2. **Authentication** > **Add a platform** > **Mobile and desktop applications** > custom redirect URI
   **`http://localhost`** > Configure.
   The browser sign-in listens on `http://localhost:<free port>`; for loopback addresses Microsoft accepts any port
   as long as `http://localhost` is registered.
3. **Authentication** > **Advanced settings** > **Allow public client flows** = **Yes**.
   This enables the "Use a code" sign-in (device code, microsoft.com/link).
4. Save. **Certificates & secrets** stays empty. **API permissions** need nothing extra: the scopes
   `XboxLive.signin offline_access` are requested at sign-in.

## Errors and what they mean

| Error | Fix |
| --- | --- |
| `redirect_uri is not valid` / AADSTS50011 (in the browser) | step 2: add `http://localhost` under Mobile and desktop applications (not under Web) |
| AADSTS7000218, `client_assertion or client_secret` | step 3: Allow public client flows = Yes |
| AADSTS700016, `not enabled for consumers`, AADSTS9002331 | step 1: personal Microsoft accounts |

Changes in Azure apply within a few minutes. The default code sign-in needs only step 3. Every sign-in error is
written to `~/.cache/BedrockLauncher/logs/launcher.log` with Microsoft's own error text.

## How the sign-in works

| Step | Endpoint |
| --- | --- |
| Code sign-in (default) | `.../devicecode` then `.../token`: the code is copied and microsoft.com/link opens; needs step 3 only |
| Browser sign-in ("Sign in with the browser instead") | `.../authorize`, authorization code + PKCE, redirect to `http://localhost:<port>`; needs step 2 |
| Xbox Live | `user.auth.xboxlive.com` (RPS ticket `d=<token>`) then `xsts.auth.xboxlive.com` (gamertag, XUID) |
| License check | `rp://api.minecraftservices.com/` XSTS > `login_with_xbox` > `entitlements/mcstore`; else the Store license through Xodus |

Only the refresh token is stored, in `~/.config/BedrockLauncher/account.json` (mode 600).

## Minecraft services approval

Mojang only lets approved Azure apps call `api.minecraftservices.com`. Until the app is approved, that check answers
"not approved" and the launcher verifies the license through Xodus (Settings > Accounts > Sign in to Xodus).
To get the app approved, send its Application ID through Mojang's form: <https://aka.ms/mce-reviewappid>.
