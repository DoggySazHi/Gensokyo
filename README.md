# Gensokyo
A (personal) CI/CD solution for managing multiple servers.
Designed for integration with GitHub Actions.

## Purpose
Gensokyo is designed for multiserver deployments, where multiple "clients" are capable of processing jobs.
The central server (Yukari) orchestrates the jobs, and the clients (Ran) execute them.

Yukari is publically accessible via authenticated HTTP APIs, while Ran will connect to Yukari via a WebSocket connection.
This means clients can be behind NATs and firewalls, and still be able to receive jobs.

## Naming
I was bored and wrote this up in an evening.