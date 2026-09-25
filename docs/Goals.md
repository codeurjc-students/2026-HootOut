# Goals:

HootOut aims to be an instant messaging application where you can chat with direct messages, or join a server and chat with many people at the same time.

## Functional Goals:

The goals of this project are that users can send messages to other users. They can send them via direct messages or in group chats called "Servers".
- Add other users as friends so you can send direct messages to them.
- Send emojis, gifs, images, videos, audio and files.
- Create or join a server and chat with multiple people at the same time.
- Organize server conversations by having different Channels.
- Manage the server permissions by creating and assigning roles to members.
- Add third party applications to your servers to extend the functionality.

## Technical goals:

The main technical challenge is the real time nature of the application. Serving messages to multiple clients, preventing synchronization issues, connecting and disconnecting clients, clients with unstable connections.

- Frontend SPA in Vue.js.
- Backend REST API and Websockets with ASP.NET Core.
- Message broker with RabbitMQ to handle real time traffic and horizontal scaling.
- PostgreSQL as the main database, and Elasticsearch for message queries.
- Automated backend and frontend testing, and code analysis with Sonar.
- GitHub Flow for git versioning and GitHub Projects with a Kanvan board for project management.
- Workflow automations with CI through GitHub Actions.
- Docker and Docker compose for deployments.