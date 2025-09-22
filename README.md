imple Training Store prototype

Open the solution in Visual Studio 2022+, and run all tests. These should include:
 - A suite of smoke tests, before which the servers are populated with a user and a course. Each test begins as a REST request to the API gateway, resulting in a MassTransit request to one or more services, all of which use the database.
   - Bare minimum smoke tests for the auth, orchestrator, order, and learner services, and for the API gateway
   - A test of user creation and authentication, which involves all four services
   - A test of order creation, which involves all four services and creates a relationship between a user and an order
   - A test of course creation, which involves the orchestrator, order, and learner services
   
