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

  

## Getting Started

  

To get this project up and running on your local machine, follow the steps below:

  

### Prerequisites

1.  **.NET 8 SDK**:

- Download and install the **.NET 8 SDK** from [here](https://dotnet.microsoft.com/download).

2.  **Visual Studio Code (VSCode)**:

- Download and install [Visual Studio Code](https://code.visualstudio.com/).

- Install the C# extension by OmniSharp for better development support in VSCode.

  

### Clone the Repository

  

Clone the repository to your local machine:

  

```bash

git  clone  

cd  bazaar
```

## Setup and Configuration

1. Open the project
2. Open a terminal in VSCode and restore the project dependencies by running: `dotnet restore`
3.  To run the project locally, use the following command: `dotnet run`, you may also use the key bindings `Ctrl+Shift+B` for building the project and `Ctrl+Shift+D` for running and debugging the project.

## Sample API usage
You may figure out more about using the API by running the project and browsing the Swagger UI at https://localhost:7176/swagger/index.html 

Using [Swagger CodeGen](https://github.com/swagger-api/swagger-codegen)  you can auto generate Client APIs for the frontend applications from https://localhost:7176/swagger/v1/swagger.json

## Testing
 Toplevel you can trigger tests in 
 
```bash
dotnet test
```

## Contributing
Please adhere to the [SI guidlines](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow)
