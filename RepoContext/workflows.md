# DigiPen.Workflows — Architecture Context for Auto-Fix

## Technology Stack
- **Backend**: ASP.NET Core 8.0 with Entity Framework Core
- **Frontend**: Vue 3 with TypeScript, Vite, Naive UI component library
- **Database**: SQL Server (EF Core Code First migrations)
- **Authentication**: OpenID Connect with cookie authentication

## File Structure Patterns

### Backend
- **Entities**: `/Entities/` — all domain models, using `BaseEntity<T>` base class
- **Controllers**: `/Controllers/` — RESTful API controllers (e.g., `OipV2Controller.cs`)
- **Services**: `/Services/` — business logic (e.g., `OipV2Service.cs`, `WorkflowInstanceService.cs`)
- **DTOs**: Defined within service or controller files, or in `/Models/` directory
- **Migrations**: `/Migrations/` — EF Core migrations
- **AutoMapper Profiles**: `/MappingProfiles/` or inline in service registration

### Frontend
- **Workflow Pages**: `/ClientApp/src/workflows/{workflowName}/` — each workflow has its own directory
  - Example: `/ClientApp/src/workflows/oipv2/` contains all OIP v2 components
- **Workflow Steps**: Vue components prefixed with `ws-` (e.g., `ws-student.vue`, `ws-advisor.vue`)
- **TypeScript Types**: `.d.ts` files in the workflow directory (e.g., `oipstudentdto.d.ts`)
- **Pages**: `/ClientApp/src/pages/` — file-based routing via vite-plugin-pages
- **Shared Components**: `/ClientApp/src/components/`
- **Stores**: `/ClientApp/src/stores/` — Pinia state management
- **API Layer**: Axios with TanStack Query for data fetching

## Frontend Patterns

### Vue Components
- Use **Composition API** with `<script setup lang="ts">`
- Component library: **Naive UI** (auto-imported) — use `n-input`, `n-form-item`, `n-select`, etc.
- Form validation: Naive UI's built-in form validation with rules
- A form field typically looks like:
  ```vue
  <n-form-item label="Field Label" path="fieldName" rule="required">
    <n-input v-model:value="formData.fieldName" placeholder="Enter value" />
  </n-form-item>
  ```

### TypeScript DTOs
- Frontend DTOs are defined as TypeScript interfaces in `.d.ts` files
- They mirror backend DTOs but use camelCase property names
- Example: `LinkedInProfileUrl` (C#) → `linkedInProfileUrl` (TS)

## Backend Patterns

### Adding a New Field End-to-End
1. **Entity**: Add property to the entity class in `/Entities/`
2. **Migration**: Create EF Core migration
3. **DTO**: Add to request/response DTOs
4. **AutoMapper**: Update mapping profile if using AutoMapper
5. **Service**: Update service logic if needed
6. **Controller**: Usually no change needed (DTOs flow through)
7. **Frontend DTO**: Add to TypeScript interface in `.d.ts`
8. **Vue Component**: Add form field using Naive UI components

### Validation
- Backend: Data annotations on DTOs (`[Required]`, `[Url]`, etc.)
- Frontend: Naive UI form rules (type: 'string', required: true, etc.)

## Key Services
- `OipV2Service` — handles OIP v2 workflow business logic
- `WorkflowInstanceService` — manages workflow instance lifecycle
- `EmailService` — notifications with HTML templates
- `ColleagueIntegrationService` — Colleague ERP integration

## Workflow System
- Workflows defined in `workflow-definitions.json`
- Each workflow has steps (student, advisor, department, etc.)
- Step components are Vue files in the workflow directory
- Workflow state is managed via `WorkflowInstance` entity
