# Software execution

## Requirements:

For running this software, Docker must be installed and running.

- Windows: Docker Desktop https://www.docker.com/products/docker-desktop/
- Linux: Docker Compose: https://docs.docker.com/compose/install/linux/

## For development:

For development, follow the README.md files included on /backend and /frontend.

## Using Docker compose OCI file:

Before running anything, you need to have some certificates for https. To generate local trusted certificates, follow the README.md on /backend.

There are some environment variables that must be setup for the application to run. There is an example .env file on /docker/.env.example

On Windows, there may be issues when using a .env file with the docker compose command. Use the following powershell command to set the environment variables from a ./.env file:

```powershell
Get-Content .env | ForEach-Object { if ($_ -match '^\s*([^#=]+)=(.*)$') { Set-Item "env:$($Matches[1].Trim())" $Matches[2].Trim() } }
```

To run the docker file, after setting the environment variables you can run this comand. Change the $tag to the version you want to run.

```sh
docker compose -f oci://docker.io/ivanmotos/hootout-compose:$tag up -d
```

Example: $tag = 1.0.0

```sh
docker compose -f oci://docker.io/ivanmotos/hootout-compose:1.0.0 up -d
```

## Using the application:

The application runs on port :80 and :443.

To have example data, set the environment variable "INIT_DATA=true". This will create a bunch of Users, Servers, Channels, and messages. If it is left to false, no example data will be created.

There is a User "adminTest" with email "adminTest@hootout.com" and password "Passwordtest123", that will be the owner of all the Servers created in this step.

There is another User "testUser" with email "testUser@hootout.com" and password "Passwordtest123". This user will be part of some Servers and will have some Server invitations.
