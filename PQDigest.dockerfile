# Use the official .NET 9.0 runtime as the base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
ARG CONFIGURATION="Development"

# Set the working directory inside the container
WORKDIR /PQDigest

# Copy PQDigest from the local published folder to the container
COPY ./[Bb]uild/${CONFIGURATION}/Applications/PQDigest/net9.0/publish/linux-x64/ /PQDigest/

# Set permissions for all copied folders and files
RUN chmod -R 777 /PQDigest

# Ensure the application is executable
RUN chmod +x /PQDigest/PQDigest

# Expose the webserver port
EXPOSE 8080

# Define the entry point to run
ENTRYPOINT ["sh", "-c", "exec /PQDigest/PQDigest"]
