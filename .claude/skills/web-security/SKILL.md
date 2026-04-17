---
name: web-security
description: Enforce web security and avoid security vulnerabilities
---

# Web Security

We treat **web security as a core requirement**, not an afterthought.
Assume hostile input and untrusted environments by default.

## Core Principles

- **NEVER** trust user input
- **ALWAYS** validate and sanitize data at boundaries
- Prefer secure defaults over configurability

## XSS & Injection

- **AVOID** `dangerouslySetInnerHTML` and raw HTML injection
- Escape and encode dynamic content properly
- Never interpolate untrusted data into HTML, CSS, or JS contexts
- Ensure SQL injection protection

## Authentication & Authorization

- Do not store secrets or tokens in insecure locations
- Always enforce authorization on the server
- Use JWT tokens and headers for authorization

## Data Handling

- Minimize data exposure
- Do not log sensitive information
- EventLog should be the source of truth for all data. It is expected that microservices that require data will subscribe to the necessary messages

## Dependencies & Supply Chain

- Avoid unnecessary packages
- Treat third-party code as untrusted input

## General Principles

- Simplicity reduces attack surface
- If unsure, choose the more restrictive option
