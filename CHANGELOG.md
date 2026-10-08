

## [0.0.2] - Unreleased

#### Added

- Added Home page
- Backend and frontend now uses HTTPS and WSS instead of HTTP and WS.
- User registration and Login. Auth with JWT access and refresh tokens.
- Frontend router redirection for Unauthorized users.
- Backend API and WebSocket secured with Authorization
- Users can send messages through WebSockets and other users subscribed to that channel will receive them.
- Saving Channels and Messages to DB.

#### Removed

- Removed temporal files and code that was used as a starting point (home page user list and ws communication)

## [0.0.1] - 2026/09/28

#### Added

- /frontend folder for Vue.js frontend project.
- /backend folder for the ASP.NET Core 10 backend solution.
- Minimal frontend page in / to show backend connection.
- Mininal backend API and Websocket services that connects to the PostgresDB.
- Initial frontend unit, component and E2E tests.
- Initial backend unit, integration and API tests. 
- Continuos Integration with Github Actions on the .github/workflows folder.
- Docker compose file to run the backend locally.
- /docs folder to keep the project documentation.
- /docs/api folder to have OpenAPI documentation.