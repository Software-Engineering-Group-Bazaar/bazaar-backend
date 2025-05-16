# Bazaar Backend Project

  

## Introduction

  

Welcome to the **Bazaar Backend Project**! This project is a modular architecture built with **.NET 8**, aimed at creating a highly maintainable and scalable application structure. The application follows a monolithic approach while ensuring that each module is decoupled and can evolve independently. It contains several domains like **Admin**, **Catalog**, **Order**, **Users**, and more, each serving a distinct responsibility in the application.

  

**Key Features**:

-  **Modular Structure**: Each module (Admin, Catalog, Order, Users, etc.) is self-contained.

-  **Scalability**: You can easily extend the app by adding new modules.

-  **API Integration**: Exposes RESTful APIs for each module to interact with other systems.

  

### Dependencies:

-  **.NET 8 SDK**: Make sure you have the .NET 8 SDK installed. You can get it from [here](https://dotnet.microsoft.com/download).

-  **Visual Studio Code**: Recommended IDE for working on the project. [Download VSCode](https://code.visualstudio.com/).

-  **Docker**: Allows building, sharing and running container applications [Download Docker](https://www.docker.com/).
  

## Getting Started

  

To get this project up and running on your local machine, follow the steps below:

  

### Prerequisites

1.  **.NET 8 SDK**:

- Download and install the **.NET 8 SDK** from [here](https://dotnet.microsoft.com/download).

2.  **Visual Studio Code (VSCode)**:

- Download and install [Visual Studio Code](https://code.visualstudio.com/).

- Install the C# extension by OmniSharp for better development support in VSCode.

3.  **Docker**:

- Download and install [Download Docker](https://www.docker.com/).

- Install the C# extension by Microsoft for better development support in VSCode.

4.  **pgadmin (Recommended, but a terminal or Dbeaver can be used instead):**

- Download and install [pgadmin](https://www.pgadmin.org/download/).

- pgAdmin provides a user-friendly interface for managing PostgreSQL databases.

- After installation, launch pgAdmin and connect to your PostgreSQL instance.

- Use the Query Tool to run SQL commands and manage your database efficiently.

### Clone the Repository

  

Clone the repository to your local machine:

  

```bash

git  clone  

cd  bazaar
```

## Setup and Configuration

1. Make sure your Docker application is open
2. Open the project in VSCode
3. Open a terminal in VSCode and run: `docker compose up`
3. Open a terminal in VSCode and restore the project dependencies by running: `dotnet restore`
4. After that, run the following commands: 
- `dotnet ef database update -c UsersDbContext`
- `dotnet ef database update --context StoreDbContext`
- `dotnet ef database update --context CatalogDbContext`
- `dotnet ef database update --context OrdersDbContext`
- `dotnet ef database update --context NotificationsDbContext`
- `dotnet ef database update --context InventoryDbContext`
- `dotnet ef database update --context ReviewDbContext`
- `dotnet ef database update --context AdDbContext`
- `dotnet ef database update --context ConversationDbContext`
- `dotnet ef database update --context TicketingDbContext`
- Any new DbContext that we add should also be run. 

If these commands don't go through, run this before trying them again: `dotnet tool install --global dotnet-ef`

5.  To run the project locally, use the following command: `dotnet run`, you may also use the key bindings `Ctrl+Shift+B` for building the project and `Ctrl+Shift+D` for running and debugging the project.

## Sample API usage
You may figure out more about using the API by running the project and browsing the Swagger UI at https://localhost:7176/swagger/index.html 

Using [Swagger CodeGen](https://github.com/swagger-api/swagger-codegen)  you can auto generate Client APIs for the frontend applications from https://localhost:7176/swagger/v1/swagger.json

## Backend with https

Run the following:

- `dotnet dev-certs https --trust`

- `dotnet run --launch-profile https`

## Testing
 Toplevel you can trigger tests in 
 
```bash
dotnet test
```

## Contributing
Please adhere to the [SI guidlines](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow)

## More Documentation
[GitFlow Guide](Gitflow.md)
