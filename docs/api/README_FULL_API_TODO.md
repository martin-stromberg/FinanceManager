# FULL API TODO

This folder contains initial controller stubs with endpoints. The next step is to generate detailed API docs for each controller including:
- Route path
- HTTP method
- Request DTO schema with examples
- Response DTO schema with examples
- Validation error codes and example ProblemDetails
- Authorization requirements

Suggested automation:
- Use reflection over controllers and attributes to build an initial OpenAPI-style listing.
- Supplement with manual descriptions for complex endpoints (booking, classification, uploads).
