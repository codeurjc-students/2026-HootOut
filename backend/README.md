### Run Docker Compose:
On the backend folder:

``` 
docker compose -f ./compose.dev.yaml up -d --build 
``` 

### Docker compose .env
Make an .env file similar to .env.example. Each line is a env variable that is used on the docker compose file. 

### Generate OpenApi .yaml file:
Inside the project folder:
```
dotnet build
```
After it runs will output "api-docs.yaml" on the project directory

### SwaggerUI:
Route: /swagger/index.html