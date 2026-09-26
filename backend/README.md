### Run Locally:

[.Net SDK 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) is required to build and run this locally

To build the project run 
```sh
dotnet build
```

### Environment variables:

Before running docker compose, create a .env file, following the .env.example file, and update the variables with your own.

### Run Docker Compose:
On the backend folder:

```sh
docker compose -f ./compose.dev.yaml up -d --build 
``` 

### Docker compose .env
Make an .env file similar to .env.example. Each line is a env variable that is used on the docker compose file. 

### Generate OpenApi .yaml file:
Inside the project folder:
```sh
dotnet build
```
After it runs will output "api-docs.yaml" on the project directory

### Generate OpenApi .html file:
Uses Redocly CLI to generate the openApi .html documentation. Requires Node.js to be installed.
Inside the project folder with a .yaml file:
```sh
npx --yes @redocly/cli build-docs './api-docs.yaml' --output './api-docs.html'
```

### SwaggerUI:
SwaggerUI is accesible on the Development environment for the Rest API
Url: host:port/swagger/index.html

### 

### Install Sonar tool:
```sh
dotnet tool install --global dotnet-coverage
dotnet tool install --global dotnet-sonarscanner
```

### Use Sonar Tool:

```sh
dotnet sonarscanner begin /d:sonar.host.url="<sonar-url>" /k:"<sonar-project-key>" /d:sonar.token="<sonar-token>" /d:sonar.cs.vscoveragexml.reportsPaths=coverage.xml
dotnet build --no-incremental
dotnet-coverage collect "dotnet test" -f xml -o "coverage.xml"
dotnet sonarscanner end /d:sonar.token="<sonar-token>"
```