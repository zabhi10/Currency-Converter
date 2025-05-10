# CurrencyConverterApi

The CurrencyConverterApi provides endpoints for currency conversion, fetching the latest exchange rates, and retrieving historical exchange rate data. It uses JWT for authentication and supports role-based access control.

## Table of Contents

1.  [Prerequisites](#prerequisites)
2.  [Running the API with Docker](#running-the-api-with-docker)
    *   [Build the Docker Image](#build-the-docker-image)
    *   [Run the Docker Container](#run-the-docker-container)
    *   [With Visual Studio / VS Code](#with-visual-studio--vs-code)
3.  [API Endpoints](#api-endpoints)
    *   [Authentication](#authentication)
    *   [Exchange Rates](#exchange-rates)
4.  [API Key and Client IDs](#api-key-and-client-ids)
5.  [Authorization](#authorization)
    *   [User Role](#user-role)
    *   [Admin Role](#admin-role)
6.  [Hosting](#hosting)
7.  [Scaling](#scaling)
    *   [Vertical Scaling](#vertical-scaling)
    *   [Horizontal Scaling](#horizontal-scaling)
8.  [Assumptions Made](#assumptions-made)
9.  [Possible Future Enhancements](#possible-future-enhancements)
10. [Checking Code Coverage](#checking-code-coverage)

## Prerequisites

*   Docker installed and running.
*   [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) installed (if using Visual Studio or VS Code).
*   Visual Studio 2022+ or Visual Studio Code with C# extension installed.
*   A tool for making HTTP requests (e.g., curl, Postman).

## Running the API with Docker

You can run the CurrencyConverterApi using Docker, Visual Studio, or Visual Studio Code.

### Build the Docker Image

1.  Navigate to the root directory of the `CurrencyConverterApi` project (where the `Dockerfile` is located). This will be `./CurrencyConverterApi/` relative to this README.
2.  Run the following command to build the Docker image:

    ```bash
    docker build -t currencyconverterapi .    
    ```

### Run the Docker Container

1.  Once the image is built, run the following command to start a container:

    ```bash
    docker run -d -p 8080:8080 --name currencyconverter-app currencyconverterapi
    ```
    *   `-d`: Runs the container in detached mode.
    *   `-p 8080:8080`: Maps port 8080 of your host machine to port 8080 of the container (where the API will be listening, as per Dockerfile EXPOSE). The application inside the container listens on HTTP. If Kestrel is configured for HTTPS on 8081 as well, you can map that too: `-p 8081:8081`.
    *   `--name currencyconverter-app`: Assigns a name to the container for easier management.
    *   `currencyconverterapi`: The name of the image you built.

The API should now be accessible at `http://localhost:8080`.

## API Endpoints

All endpoints are prefixed with `/api/v1`.

### Authentication

#### 1. Get Token

*   **Endpoint:** `POST /Auth/token`
*   **Description:** Authenticates a client and returns a JWT token.
*   **Access:** Anonymous
*   **Request Body:** `application/json`
    ```json
    {
      "clientId": "your_client_id",
      "apiKey": "demo_api_key"
    }
    ```
*   **Example Call (curl):**
    ```bash
    curl -X POST "http://localhost:8080/api/v1/Auth/token" \
         -H "Content-Type: application/json" \
         -d '{"clientId": "user123", "apiKey": "demo_api_key"}'
    ```
*   **Success Response (200 OK):**
    ```json
    {
      "accessToken": "your_jwt_token",
      "expiresIn": 900, // in seconds
      "issuedAt": "2025-05-11T10:00:00Z",
      "expiresAt": "2025-05-11T10:15:00Z"
    }
    ```

#### 2. Get Current User Information

*   **Endpoint:** `GET /Auth/me`
*   **Description:** Returns information about the currently authenticated user based on the provided JWT.
*   **Access:** Authenticated User (requires a valid JWT in the `Authorization` header)
*   **Example Call (curl):**
    ```bash
    curl -X GET "http://localhost:8080/api/v1/Auth/me" \
         -H "Authorization: Bearer your_jwt_token"
    ```
*   **Success Response (200 OK):**
    ```json
    {
      "clientId": "user123",
      "roles": ["User"],
      "isAuthenticated": true,
      "expiresAt": "1746907200" // Unix timestamp for token expiry
    }
    ```

### Exchange Rates

All endpoints below require a valid JWT token passed in the `Authorization` header as `Bearer your_jwt_token`.

#### 1. Get Latest Rates

*   **Endpoint:** `GET /Rates/latest`
*   **Description:** Gets the latest exchange rates for a specific base currency.
*   **Access:** User Role
*   **Query Parameters:**
    *   `BaseCurrency` (string, required, 3-letter ISO code): e.g., `USD`
*   **Example Call (curl):**
    ```bash
    curl -X GET "http://localhost:8080/api/v1/Rates/latest?baseCurrency=USD" \
         -H "Authorization: Bearer your_jwt_token"
    ```
*   **Success Response (200 OK):**
    ```json
    {
      "base": "USD",
      "date": "2025-05-11T12:00:00Z",
      "rates": {
        "EUR": 0.92,
        "GBP": 0.79,
        // ... other currencies
      }
    }
    ```

#### 2. Convert Currency

*   **Endpoint:** `GET /Rates/convert`
*   **Description:** Converts an amount from one currency to another.
*   **Access:** User Role
*   **Query Parameters:**
    *   `BaseCurrency` (string, required, 3-letter ISO code): e.g., `USD`
    *   `TargetCurrency` (string, required, 3-letter ISO code): e.g., `EUR`
    *   `Amount` (decimal, required): e.g., `100`
*   **Example Call (curl):**
    ```bash
    curl -X GET "http://localhost:8080/api/v1/Rates/convert?BaseCurrency=USD&TargetCurrency=EUR&Amount=150" \
         -H "Authorization: Bearer your_jwt_token"
    ```
*   **Success Response (200 OK):**
    ```json
    {
      "base": "USD",
      "target": "EUR",
      "amount": 150,
      "convertedAmount": 138.50,
      "date": "2025-05-11T12:05:00Z"
    }
    ```

#### 3. Get Historical Rates

*   **Endpoint:** `GET /Rates/history`
*   **Description:** Retrieves historical exchange rates for a given period with pagination.
*   **Access:** Admin Role
*   **Query Parameters:**
    *   `BaseCurrency` (string, required, 3-letter ISO code): e.g., `USD`
    *   `StartDate` (date, required, YYYY-MM-DD): e.g., `2023-01-01`
    *   `EndDate` (date, required, YYYY-MM-DD): e.g., `2023-01-31`
    *   `Page` (int, optional, default: 1): Page number for pagination.
    *   `PageSize` (int, optional, default: 10): Number of items per page.
*   **Example Call (curl):**
    ```bash
    curl -X GET "http://localhost:8080/api/v1/Rates/history?BaseCurrency=GBP&Start=2023-05-01&End=2023-05-05&Page=1&PageSize=2" \
         -H "Authorization: Bearer your_admin_jwt_token"
    ```
*   **Success Response (200 OK):**
    ```json
    {
      "base": "GBP",
      "startDate": "2023-05-01T00:00:00Z",
      "endDate": "2023-05-05T00:00:00Z",
      "page": 1,
      "pageSize": 2,
      "totalItems": 5, // Example total
      "totalPages": 3, // Example total
      "data": [
        { "date": "2023-05-01T00:00:00Z", "rates": { "USD": 1.25, "EUR": 1.15 } },
        { "date": "2023-05-02T00:00:00Z", "rates": { "USD": 1.26, "EUR": 1.16 } }
      ]
    }
    ```

## API Key and Client IDs

*   **API Key:** Use `demo_api_key` when requesting a token. This is configured in `CurrencyConverterApi/appsettings.Test.json` (and other environment-specific `appsettings.*.json` files within the `CurrencyConverterApi` project).
*   **Client IDs:**
    *   To get a **User** role token, use any `clientId` that does not contain "admin" (e.g., `user123`, `client_abc`).
    *   To get an **Admin** role token, use a `clientId` that contains "admin" (e.g., `admin_user`, `main_admin_client`).

## Authorization

Access to endpoints is controlled by JWT roles.

### User Role

*   Clients with the "User" role (or "Admin" role, as Admin supersedes User) can access:
    *   `GET /api/v1/Auth/me`
    *   `GET /api/v1/Rates/latest`
    *   `GET /api/v1/Rates/convert`

### Admin Role

*   Clients with the "Admin" role can access all User role endpoints plus:
    *   `GET /api/v1/Rates/history`

## Hosting

This ASP.NET Core API can be hosted in various environments:

*   **Container Orchestration Platforms:** Kubernetes, Docker Swarm, Azure Kubernetes Service (AKS), Amazon Elastic Kubernetes Service (EKS), Google Kubernetes Engine (GKE). These are recommended for production for managing deployments, scaling, and resilience.
*   **Cloud PaaS:** Azure App Service, AWS Elastic Beanstalk, Google App Engine. These platforms simplify deployment and management.
*   **Virtual Machines:** On any cloud provider (Azure, AWS, GCP) or on-premises, running a supported OS (Linux, Windows). You would need to manage the .NET runtime installation and web server (like Nginx or Apache as a reverse proxy).
*   **Serverless:** While this traditional API structure isn't directly serverless, parts could be refactored into Azure Functions or AWS Lambda if desired for specific event-driven scenarios.

When hosting, ensure:
*   **Configuration Management:** Use environment variables or configuration services (like Azure App Configuration, AWS Systems Manager Parameter Store) to manage `appsettings.json` values, especially secrets. The `ASPNETCORE_ENVIRONMENT` variable (e.g., `Production`, `Development`) will determine which `appsettings.{Environment}.json` is loaded from the `CurrencyConverterApi` project.
*   **Logging:** Configure robust logging (e.g., Serilog writing to a centralized logging solution like Elasticsearch, Splunk, Azure Monitor). The Dockerfile already sets up a logs directory.
*   **HTTPS:** Ensure HTTPS is enforced in production. This typically involves configuring a reverse proxy (Nginx, Apache) or using the built-in HTTPS capabilities of PaaS offerings.

## Scaling

### Vertical Scaling

*   **Description:** Increasing the resources of a single instance (e.g., more CPU, RAM).
*   **How:**
    *   In cloud environments, this usually means choosing a larger VM size or a higher service tier for PaaS offerings.
    *   For Docker containers, you can allocate more resources to the container if the host machine has them.
*   **Considerations:** There's a physical limit to how much you can scale vertically. It can also lead to downtime during resizing if not managed carefully.

### Horizontal Scaling

*   **Description:** Adding more instances of the application.
*   **How:**
    *   Run multiple Docker containers of the API.
    *   Use a load balancer (e.g., Nginx, HAProxy, or cloud-provided load balancers like Azure Load Balancer, AWS Application Load Balancer) to distribute traffic across instances.
*   **Considerations for this API:**
    *   **Statelessness:** The API appears to be largely stateless (authentication is token-based, requests are self-contained). This is ideal for horizontal scaling.
    *   **Caching:** The `CachedCurrencyService` implies caching. For horizontal scaling with in-memory caching, each instance would have its own cache. This can lead to inconsistencies if not managed or if a distributed cache is preferred.
        *   **Distributed Cache:** Consider using a distributed cache like Redis or Memcached if cache consistency across all instances is critical or if you want to offload cache memory from the application instances. This would require changes to `CachedCurrencyService` to use the distributed cache provider.
    *   **Rate Limiting:** The current rate limiting is in-memory. For effective rate limiting across horizontally scaled instances, a distributed store (like Redis) would be needed to maintain shared rate limit counters.
    *   **Database/External Services:** Ensure any backing services (like the Frankfurter API) can handle the increased load from multiple instances.

## Assumptions Made

*   The primary external dependency for exchange rates is the Frankfurter API (`https://api.frankfurter.app`).
*   The API key (`demo_api_key`) is intended for demonstration/testing and is shared. In a production scenario, each client would have a unique, securely managed API key. This key is configured in `CurrencyConverterApi/appsettings.Test.json`.
*   Client role ("User" or "Admin") is determined by the presence of "admin" in the `clientId` string during token generation. In a real system, this would be managed through a proper identity and access management (IAM) system or user database.
*   The JWT secret key in `CurrencyConverterApi/appsettings.Test.json` is for testing; production keys must be strong, unique, and securely managed (e.g., via Azure Key Vault, HashiCorp Vault).
*   The application is designed to run in a containerized environment, as indicated by the Dockerfile at `CurrencyConverterApi/Dockerfile`.
*   Logging to a file in `/app/logs` (inside the container) is suitable for containerized environments where logs can be collected by a log aggregator.

## Possible Future Enhancements

*   **Production-Grade Authentication/Authorization:**
    *   Integrate with an OAuth 2.0 / OpenID Connect provider (e.g., Okta) for robust user management and authentication.
    *   Implement more granular permissions beyond simple "User" and "Admin" roles.
*   **Distributed Caching:** Implement a distributed cache (e.g., Redis) for `CachedCurrencyService` to ensure cache consistency and improve performance in a horizontally scaled environment.
*   **Distributed Rate Limiting:** Move rate-limiting logic to use a distributed store (e.g., Redis) for accurate enforcement across multiple instances.
*   **Webhook Support:** Allow clients to subscribe to notifications for currency rate updates.
*   **Enhanced Input Validation:** Add more specific validation rules (e.g., list of supported currency codes).
*   **Real-time Rate Updates:** For applications requiring real-time rates, integrate with a streaming data source or use WebSockets.
*   **Health Check Endpoint Improvements:** Enhance the default ASP.NET Core health check endpoint (`/healthz` or similar) to include checks for critical dependencies (e.g., Frankfurter API connectivity, database).
*   **More Sophisticated API Key Management:** Implement a system for clients to securely manage their API keys.

## Checking Code Coverage

This project is set up to generate code coverage reports when tests are run using the provided shell script.

1.  **Navigate to the API Project Directory:**

    Ensure your terminal is in the `CurrencyConverterApi` directory. If your current directory is the solution root, use:

    ```bash
    cd ./CurrencyConverterApi
    ```

2.  **Ensure the script is executable:**

    If you haven't already, make the `run_tests_with_coverage.sh` script executable. You only need to do this once. From the `CurrencyConverterApi` directory. Use git bash on windows:

    ```bash
    chmod +x run_tests_with_coverage.sh
    ```
    Or from the solution root:
    ```bash
    chmod +x ./CurrencyConverterApi/run_tests_with_coverage.sh
    ```

3.  **Run the test and coverage script:**

    Execute the script from the `CurrencyConverterApi` directory:

    ```bash
    ./run_tests_with_coverage.sh
    ```
    Alternatively, from the solution root:
    ```bash
    ./CurrencyConverterApi/run_tests_with_coverage.sh
    ```

    This script performs the following actions:

    *   Cleans up any previous coverage files.
    *   Runs `dotnet test` on the `CurrencyConverterApi.Tests` project (located at `./CurrencyConverterApi.Tests/` relative to the script).
    *   Collects coverage information using Coverlet, outputting a `cobertura.xml` file to the `CurrencyConverterApi.Tests` directory.
    *   The script is configured to check for a coverage threshold (e.g., 90% line coverage as seen in the script) and excludes test projects and xUnit attributes from coverage metrics.
    *   Installs or updates `dotnet-reportgenerator-globaltool` if not already present or up-to-date.
    *   Uses `reportgenerator` to convert the `cobertura.xml` file into an HTML report.
    *   The HTML report is generated in the `CurrencyConverterApi/coverage-report` directory (relative to the solution root).

4.  **View the HTML Report:**

    After the script completes successfully, open the `index.html` file from the generated report directory in your web browser to view the detailed code coverage. The path relative to the solution root is:

    ```plaintext
    ./CurrencyConverterApi/coverage-report/index.html
    ```

    (You can typically use `open ./CurrencyConverterApi/coverage-report/index.html` on macOS from the solution root to open it in your default browser).

    ![alt text](assets/code-coverage.png)