# Delivery Platform

Plataforma de delivery desenvolvida com **C# e .NET 10**, estruturada para suportar múltiplos restaurantes, utilizadores e permissões específicas por restaurante.

O projecto adopta uma arquitectura em camadas, com separação entre regras de negócio, casos de uso, infraestrutura e exposição HTTP.

> **Estado do projecto:** em desenvolvimento activo. A implementação actual inclui autenticação, gestão de restaurantes e memberships.

## Tecnologias

- **.NET 10 / ASP.NET Core**
- **C#**
- **Entity Framework Core 10**
- **PostgreSQL 18**
- **ASP.NET Core Identity**
- **Docker Compose**
- **xUnit**
- **Testcontainers**

## Arquitectura

A solução está dividida em seis projectos:

| Projecto | Responsabilidade |
|---|---|
| `Delivery.Domain` | Entidades, objectos de valor e regras de negócio |
| `Delivery.Application` | Casos de uso, contratos e lógica de aplicação |
| `Delivery.Infrastructure` | Persistência, Entity Framework Core e implementações dos repositórios |
| `Delivery.Api` | Endpoints HTTP, autenticação e composição da aplicação |
| `Delivery.UnitTests` | Testes unitários das regras de domínio e casos de uso |
| `Delivery.IntegrationTests` | Testes de integração da API e persistência |

### Fluxo de dependências

```mermaid
flowchart TB
    subgraph Presentation["Apresentação"]
        API["Delivery.Api<br/>Endpoints HTTP<br/>Autenticação e autorização"]
    end

    subgraph ApplicationLayer["Aplicação"]
        APP["Delivery.Application<br/>Casos de uso<br/>Contratos e serviços"]
    end

    subgraph DomainLayer["Domínio"]
        DOMAIN["Delivery.Domain<br/>Entidades<br/>Value Objects<br/>Regras de negócio"]
    end

    subgraph InfrastructureLayer["Infraestrutura"]
        INFRA["Delivery.Infrastructure<br/>Entity Framework Core<br/>Repositórios e persistência"]
        DB[("PostgreSQL")]
    end

    API --> APP
    API --> INFRA
    APP --> DOMAIN
    INFRA --> APP
    INFRA --> DOMAIN
    INFRA --> DB

    classDef presentation fill:#1e3a5f,color:#ffffff,stroke:#60a5fa,stroke-width:1.5px
    classDef application fill:#164e63,color:#ffffff,stroke:#22d3ee,stroke-width:1.5px
    classDef domain fill:#365314,color:#ffffff,stroke:#a3e635,stroke-width:1.5px
    classDef infrastructure fill:#713f12,color:#ffffff,stroke:#fbbf24,stroke-width:1.5px
    classDef database fill:#374151,color:#ffffff,stroke:#9ca3af,stroke-width:1.5px

    class API presentation
    class APP application
    class DOMAIN domain
    class INFRA infrastructure
    class DB database
```

O domínio não depende de frameworks de infraestrutura. A camada de aplicação define os casos de uso e os contratos necessários, enquanto a infraestrutura fornece as respectivas implementações.

## Funcionalidades implementadas

### Autenticação

- Registo de utilizadores.
- Login com email e password.
- Logout.
- Consulta do utilizador autenticado.
- Autenticação baseada em cookies HttpOnly.
- Protecção contra CSRF através de antiforgery tokens.
- Controlo de acesso através de autorização.

### Restaurantes

- Criação de restaurantes.
- Listagem dos restaurantes associados ao utilizador autenticado.
- Consulta dos detalhes de um restaurante.
- Memberships entre utilizadores e restaurantes.
- Papéis de `Owner`, `Manager` e `Staff`.
- Estados de membership: `Invited`, `Active` e `Suspended`.

Um utilizador pode estar associado a vários restaurantes. A autorização e o papel do utilizador são determinados individualmente para cada restaurante.

## Requisitos

Antes de executar o projecto, instala:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker Engine](https://docs.docker.com/engine/install/)
- [Docker Compose](https://docs.docker.com/compose/install/)

Para executar as migrations manualmente, também será necessário o Entity Framework Core CLI.

## Configuração local

### 1. Clonar o repositório

```bash
git clone https://github.com/ffakira/delivery-platform.git

cd delivery-platform
```

### 2. Configurar as variáveis de ambiente

Cria um ficheiro `.env` na raiz do repositório:

```env
# PostgreSQL
POSTGRES_PASSWORD=change_me_postgres
ConnectionStrings__DefaultConnection=Host=localhost;Port=5434;Database=delivery;Username=delivery;Password=change_me_postgres

# Redis
REDIS_PASSWORD=change_me_redis

# CORS
Cors__AllowedOrigins__0=http://localhost:5173
```

Substitui os valores de password por valores próprios.

O endereço CORS apresentado é um exemplo para um frontend local executado com Vite. Ajusta-o caso utilizes outra origem.

**Não partilhes o ficheiro `.env` nem coloques credenciais reais no controlo de versões.**

### 3. Iniciar os serviços

```bash
docker compose up -d
```

O Compose disponibiliza:

| Serviço | Endereço local |
|---|---|
| PostgreSQL | `localhost:5434` |
| Redis | `localhost:6381` |

Para verificar os serviços:

```bash
docker compose ps
```

Para consultar os logs:

```bash
docker compose logs -f
```

### 4. Instalar o Entity Framework CLI

```bash
dotnet tool install --global dotnet-ef --version 10.0.14
```

Se já estiver instalado:

```bash
dotnet tool update --global dotnet-ef --version 10.0.14
```

### 5. Aplicar as migrations

Na raiz do repositório:

```bash
dotnet ef database update \
  --project Delivery.Infrastructure \
  --startup-project Delivery.Api
```

As migrations estão localizadas em:

```text
Delivery.Infrastructure/
└── Persistence/
    └── Migrations/
```

### 6. Executar a API

```bash
dotnet run --project Delivery.Api
```

A configuração de desenvolvimento disponibiliza:

- HTTP: `http://localhost:5049`
- HTTPS: `https://localhost:7235`

O documento OpenAPI pode ser consultado em:

```text
https://localhost:7235/openapi/v1.json
```

O endpoint OpenAPI é disponibilizado apenas no ambiente de desenvolvimento.

## Executar os testes

### Testes unitários

```bash
dotnet test Delivery.UnitTests
```

### Testes de integração

```bash
dotnet test Delivery.IntegrationTests
```

Os testes de integração utilizam Testcontainers para criar uma instância isolada de PostgreSQL. É necessário ter o Docker em execução.

### Executar todos os testes

```bash
dotnet test DeliveryPlatform.slnx
```

### Compilar a solução

```bash
dotnet build DeliveryPlatform.slnx
```

## Endpoints disponíveis

Os endpoints da API estão organizados por grupos.

### Autenticação

| Método | Endpoint | Descrição | Autenticação |
|---|---|---|---|
| `GET` | `/api/auth/csrf-token` | Obter token CSRF | Não |
| `POST` | `/api/auth/register` | Registar utilizador | Não |
| `POST` | `/api/auth/login` | Iniciar sessão | Não |
| `POST` | `/api/auth/logout` | Terminar sessão | Não |
| `GET` | `/api/auth/me` | Consultar utilizador autenticado | Sim |

### Restaurantes

| Método | Endpoint | Descrição | Autenticação |
|---|---|---|---|
| `POST` | `/api/restaurants/` | Criar restaurante | Sim |
| `GET` | `/api/restaurants/` | Listar os meus restaurantes | Sim |
| `GET` | `/api/restaurants/{restaurantId}` | Consultar detalhes do restaurante | Sim |

Os pedidos `POST` protegidos por antiforgery necessitam do cookie CSRF e do header `X-CSRF-TOKEN`.

## Estrutura do repositório

```text
DeliveryPlatform/
├── Delivery.Api/
│   ├── Endpoints/
│   ├── Extensions/
│   └── Properties/
├── Delivery.Application/
│   └── Restaurants/
├── Delivery.Domain/
│   └── Restaurants/
├── Delivery.Infrastructure/
│   ├── Identity/
│   └── Persistence/
│       ├── Configurations/
│       ├── Migrations/
│       └── Repositories/
├── Delivery.IntegrationTests/
│   ├── Infrastructure/
│   └── Restaurants/
├── Delivery.UnitTests/
│   └── Restaurants/
├── compose.yml
└── DeliveryPlatform.slnx
```

## Desenvolvimento

O projecto segue alguns princípios de engenharia:

- Separação entre domínio, aplicação e infraestrutura.
- Regras de negócio independentes de persistência.
- Casos de uso explícitos.
- Persistência através de Entity Framework Core.
- Testes unitários para validar regras e comportamentos.
- Testes de integração para validar os fluxos reais da aplicação.
- Autorização baseada na membership do utilizador em cada restaurante.

## Licença

Este projecto encontra-se em desenvolvimento. A licença ainda não foi definida.