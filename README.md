# Module5_Back_End_Development_with_NET
The User Management API is a robust RESTful web service developed for TechHive Solutions to streamline employee data management across their HR and IT departments. This API provides comprehensive CRUD (Create, Read, Update, Delete) operations for managing user records with enterprise-grade security, logging, and error handling features.

# API Endpoints
- Public Endpoints
- POST   /api/auth/login     - Authenticate and receive JWT token
- POST   /api/auth/refresh   - Refresh expired token

# Protected Endpoints (Require Authentication)
- GET    /api/users          - Get all users (with filters & pagination)
- GET    /api/users/{id}     - Get specific user by ID
- POST   /api/users          - Create new user
- PUT    /api/users/{id}     - Update existing user
- DELETE /api/users/{id}     - Soft delete (deactivate) user
- PATCH  /api/users/{id}/activate  - Reactivate user