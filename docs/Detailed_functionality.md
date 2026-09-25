# Detailed Functionality:

## Basic Functionality:

- Unregistered users will have access only to public facing pages, such as the Landing Page, Register and Login.
- Users can register with an email (unique), username (unique) and a password. They will be able to upload a profile picture after registration.
- Users will need to confirm their email address to activate their account.
- Users can send a friend request to another user by username.
- Two users that are friends can send direct messages between them.
- Users can create a group chat, called "Server. The Server will have a server name, a short description and a Server picture.
- The creator of the Server will be able to edit name, description and picture. They will be able to delete the Server.
- Users that join a Server, will be able to add users to a Server.
- Users inside a Server will be able to create, edit and delete Channels inside a Server. A Channel will only have a name.
- Users will be able to send messages inside a Channel of a Server they are part of.
- All messages will have the username, the profile picture, the local time when the message was stored on the server and the message content.
- Messages can include text, pictures, GIFs, videos, audio files, and documents. The user will be able to download them.
- Messages composed of text, pictures, GIFs, videos or audio will be displayed on the applications. 
- Users will have a connection status. These statuses are connected or disconnected. Users will be able to see friend and Server members status.
- Messages sent on a Channel will be sent to all the Server members with status connected.
- When a disconnected user connects, it will receive the last messages of a Server. After that, the user will be able to fetch older messages in a paginated way.

## Intermediate Functionality:

- Users will be able to upload custom Emojis and Stickers to a Server.
- Introduction of Rol and permissions on Servers.
- Users will be able to create Roles in a Server. A role will have a name and a list of permissions.
- There will be a fixed set of permissions for Servers and Channels. Examples are:
  - Edit/delete a Server.
  - Create, edit and delete a Channel
  - Delete other User messages on the Server.
  - Invite/kick Users from a Server.
- When creating a Server, the User will have the role "Server Admin" with all the permissions.
- When a user joins a Server, he will have a default Rol "member".
- A User can have multiple roles in a Server.
- Messages will be encripted at rest.

## Advanced Functionality: 

- Integration with "Bots" and Third-Party Applications.
- Authorization and Authentication of Bots will be made via tokens.
- A Bot will be able to join a Server as a member and will have the rol "Bot".
- Bots can have additional roles like any other member.
- Users will be able to send commands on a Server on the chat. Commands will start with "/".
- Third Party Applications will be able to register commands to a Server.
- Bots can use the API to interact with the Server they are part of.
- Bots will be able to send and receive messages in real time. (Either through websockets or some type of Endpoint Callback/Webhook).