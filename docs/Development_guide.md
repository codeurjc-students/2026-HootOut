# Development Guide:

## Introduction:

HootOut is a distributed web application, the client application is called frontend and all the server services are called backend. 

The frontend is a Single Page Application ([SPA](https://developer.mozilla.org/en-US/docs/Glossary/SPA)) with [Vue.js 3](https://vuejs.org/).

The backend will consist of two applications built with [ASP.NET Core 10](https://dotnet.microsoft.com/en-us/apps/aspnet). One will be a REST API server and the other a WebSocket server. We will use RabbitMQ as a message broker to handle communication between servers. The main database will be PostgreSQL and later we will include Elasticsearch for searchs.

| Servcie | Type | Technologies | Tools | Quality Assurance | Deployment | Development Proccess |
|---------|------|--------------|-------|-----------------|------------|----------------------|
| Fronted | SPA  | Vue.js 3, HTML, CSS, JS and TS| Visual Studio Code | Vitest, Playwright, Sonar | HTML, CSS and JS artifact | Iterative and incremental with git, CI with Github Actions |
| Backend API | REST API | ASP.NET Core 10 | Visual Studio, Visual Studio Code | XUnit, TestContainers, Microsoft.AspNetCore.Mvc.Testing Sonar | Docker Image | Iterative and incremental with git, CI with Github Actions |
| Backend WebSockets | WebSocket Server | ASP.NET Core 10 | Visual Studio, Visual Studio Code | XUnit, TestContainers, Microsoft.AspNetCore.Mvc.Testing Sonar | Docker Image | Iterative and incremental with git, CI with Github Actions |
| PostgreSQL DB | Relational DB | PostgreSQL v18.6 | pgadmin 4 | | Docker Image, dedicated service | Iterative and Incremental |
| RabbitMQ | Message Broker | RabbitMQ 4.3.6 | Management Plugin | | Docker Image, dedicated service | Iterative and Incremental |

## Technologies:

### Frontend: 

#### [Vue.js](https://vuejs.org/)

Vue is a JavaScript framework for building user interfaces. It builds on top of standard HTML, CSS, and JavaScript and provides a declarative, component-based programming model.

We will use Vue's [Composition API](https://vuejs.org/guide/introduction.html#composition-api) and [Singe-File Componets](https://vuejs.org/guide/scaling-up/sfc.html).

#### [TypeScript](https://www.typescriptlang.org/)

TypeScript is a strongly typed programming language that builds on JavaScript. We will use Vue with Typescript to write type-safe components.

#### [Vite](https://vite.dev/)

Vite is a build tool that aims to provide a faster and leaner development experience for modern web projects. Consists on a dev server that provides rich feature enhancements over native ES modules, for example extremely fast Hot Module Replacement (HMR) and a build command that bundles the code to output highly optimized static assets for production.

### Backend:

#### [ASP.NET Core 10](https://dotnet.microsoft.com/en-us/apps/aspnet) 

.NET is a developer platform made up of tools, programming languages, and libraries for building many different types of applications.

ASP.NET Core extends the .NET developer platform with tools and libraries specifically for building web apps. It's Cross-platform and Open source, and we will use it with C#.

#### [PostgreSQL](https://www.postgresql.org/)

PostgreSQL is a powerful, open source object-relational database system that uses and extends the SQL language combined with many features that safely store and scale the most complicated data workloads.

PostgreSQL has earned a strong reputation for its proven architecture, reliability, data integrity, robust feature set, extensibility, and the dedication of the open source community behind the software to consistently deliver performant and innovative solutions. PostgreSQL runs on all major operating systems, has been ACID-compliant since 2001, and has powerful add-ons

#### [Autofac](https://autofac.org/)

Autofac is an addictive Inversion of Control container for .NET Core, ASP.NET Core and more.

#### [Npgsql](https://www.npgsql.org/) 

Npgsql is an open source ADO.NET Data Provider for PostgreSQL, it allows programs written in C#, Visual Basic, F# to access the PostgreSQL database server. It is implemented in 100% C# code, is free and is open source.

#### [Dapper](https://github.com/dapperlib/dapper)

Dapper is an open-source object-relational mapping (ORM) library for .NET and .NET Core applications. The library allows developers to quickly and easily access data from databases without the need to write tedious code. Dapper allows you to execute raw SQL queries, map the results to objects, and execute stored procedures, among other things. It is available as a NuGet package.

Dapper is lightweight and fast, making it an ideal choice for applications that require low latency and high performance.

#### [RabbitMQ](https://www.rabbitmq.com/)

RabbitMQ is a powerful, enterprise grade open source messaging and streaming broker that enables efficient, reliable and versatile communication for applications — perfect for distributed microservices, real-time data, and IoT. RabbitMQ is Free and Open Source

## Tools:

#### [Visual Studio Code](https://code.visualstudio.com/)

Visual Studio Code is an IDE developed by Microsoft for Windows, Linux, macOS and web browsers. It has extensions to add functionality and to support most programming languages. 

Features include support for debugging, syntax highlighting, intelligent code completion, snippets, code refactoring, and embedded version control with Git. Visual Studio Code provides a fully featured integrated terminal that opens at the root of the current workspace

#### [Visual Studio 2026](https://visualstudio.microsoft.com/es/)

Visual Studio is and IDE developed by Microsoft. It supports syntax highlighting and code completion using IntelliSense for variables, functions, methods, loops, and LINQ queries. Visual Studio includes a debugger that works both as a source-level debugger and as a machine-level debugger. Visual Studio allows developers to write extensions for Visual Studio to extend its capabilities. These extensions "plug into" Visual Studio and extend its functionality. Extensions come in the form of macros, add-ins, and packages. 

#### [OpenAPI V3](https://www.openapis.org/)

The OpenAPI Specification (OAS) defines a standard, programming language-agnostic interface description for HTTP APIs, which allows both humans and computers to discover and understand the capabilities of a service without requiring access to source code, additional documentation, or inspection of network traffic. 

## Architecture:



### Deployment:


### REST API Documentation

The REST API will be documented with OpenAPI, and can be found in this repository on the [/docs/api folder](/docs/api/).

[An online version can be checked at raw.github following this link](https://raw.githack.com/codeurjc-students/2026-HootOut/main/docs/api/api-docs.html).

## Quality Assurance:

### Automatic Testing:
Automatic testing is in place for both backend and frontend. 

- Unit tests are meant to test isolated pieces of code (functions, classes) without dependencies.
- Integration tests are meant to test larger parts of the aplication (services, workflows) and their interaction with their dependencies. These tests can interact with external components (like databases). In our backend tests, these external components will be managed with TestContainers.
- Backend API tests are meant to test api endpoints (urls) rather than internal parts of the application. They are used to validate the content of the replies/responses. 
- Frontend component tests are used to validate Vue.js components behavior mocking the backend API.
- Frontend E2E tests are used to validate the browser behavior of the frontend, making API requests to the backend.

**Since the project is still on a very early stage of development, most of the existing tests are meant to be a guide for future testing**

### Frontend:

#### Unit Testing and Component Testing:

We will use [Vitest](https://vitest.dev/) for Unit and Component testing. We will also use @vue/test-utils.

Test can be run with npm:
```sh
npm run test:unit
```
And this will be the ouptut at this stage.
![vitest terminal output](/docs/images/tests/Frontend-vitest.png)

#### E2E Testing with Playwright:
We will use [Playwright](https://playwright.dev/) for E2E testing. These tests require a the backend to be running. They will generate at the end a test report, and will run the tests on various browsers.

![playwright terminal output](/docs/images/tests/Frontend-playwright-1.png)

After running ```npx playwright show-report``` we can open the report on the browser
![playwright test report 1](/docs/images/tests/Frontend-playwright-2.png)

Here we test that the user list on the page is not empty.
![playwright test report 1](/docs/images/tests/Frontend-playwright-3.png)

Here we tests the interaction with the webSocket server, where it returns "HELLO FROM THE SERVER " + the messaged to uppercase.
![playwright test report 1](/docs/images/tests/Frontend-playwright-4.png)


### Backend:





## Development process:

## Code Editing and Execution:
