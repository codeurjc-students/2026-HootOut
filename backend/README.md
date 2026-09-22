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
Route: /swagger/index.html

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