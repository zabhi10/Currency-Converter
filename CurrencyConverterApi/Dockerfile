FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the code
COPY . ./
RUN dotnet publish CurrencyConverterApi.csproj -c Release -o out --no-restore

# Build runtime image using Alpine variant of ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app

# Copy published output from the build stage
COPY --from=build /app/out .

# Environment variable for selecting configuration (can be overridden at runtime)
ENV ASPNETCORE_ENVIRONMENT=Test

# Install CA certificates for SSL/TLS
RUN apk --no-cache add ca-certificates

# Create directory for logs and set it as a volume
# Ensure the directory is writable by the non-root user .NET images often use
RUN mkdir -p /app/logs && \
    chown app:app /app/logs
VOLUME /app/logs

# Expose ports (standard HTTP and HTTPS)
EXPOSE 8080
EXPOSE 8081

# Set user for security best practices (aspnet images create an 'app' user)
USER app

ENTRYPOINT ["dotnet", "CurrencyConverterApi.dll"]