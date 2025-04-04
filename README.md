# SchemaQuench
![.Build validate](https://github.com/Schema-Smith/SchemaSmithyFree/actions/workflows/continuous-integration.yml/badge.svg)

SchemaQuench is an opinionated, state based, database migration tool.  Similar in concept to HashiCorp's Terraform, SchemaQuench takes the desired end state for a set of databases in the form of metadata and transforms whatever server it is applied to making it match that state.   

### Why not just use migrations to maintain a server?

Migrations show the evolution of a database over time.  While that may be fine for seeing how a database has progressed, you can not tell what the current state of that database is at this very moment.  That is where a state based approach is superior.  

The state of your metadata repository at the time of your last release is an exact representation of what your server should be.  Going with this approach, treats your sql server code like any other production code, guaranteeing that they are always in sync.   

### Technical Notes

> Target Frameworks: net9.0, net481
> 
> IDEs: Visual Studio 2022, JetBrains Rider
> 
> MSSQL Server: Currently testing against 2019-CU27-ubuntu-20.04 but should work for any version

### Quick start

If you have docker, you can run 

> docker compose up

from the root of the project and the [Test Product](TestProducts/ValidProduct/Product.json) will be applied to a linux sql 2019 docker container.  You can connect to the server at localhost with the user, password and port defined in [.env](.env).

Please see the wiki for documentation on how to set up a repository for your metadata and how apply it.
