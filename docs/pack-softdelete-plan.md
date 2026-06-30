# Plano: Publicar SoftDelete como NuGet

## Pacotes

| Projeto | PackageId | Tipo |
|---------|-----------|------|
| `AuditLog.EntityFrameworkCore.SoftDelete` | `AuditLog.EntityFrameworkCore.SoftDelete` | lib |
| `AuditLog.Generator.SoftDelete` | `AuditLog.Generator.SoftDelete` | analyzer (Roslyn) |

`AuditLog.Generator.SoftDelete` depende de **`AuditLog.Generator.Shared`** (linked source, não publicado separadamente).

---

## Pré-requisito: resolver `ApplyConfigurationsFromAssembly`

Antes de publicar, preciso que o `ApplyConfigurationsFromAssembly` funcione no generator.

---

## Arquivos a modificar/criar

| # | Arquivo | Ação |
|---|---------|------|
| 1 | `src/AuditLog.EntityFrameworkCore.SoftDelete/*.csproj` | **Modificar** — adicionar `PackageId`, `Description` |
| 2 | `src/AuditLog.Generator.SoftDelete/*.csproj` | **Modificar** — adicionar `PackageId`, `Description`, `NoPackageAnalysis` |
| 3 | `src/AuditLog.Generator.SoftDelete/build/AuditLog.Generator.SoftDelete.props` | **Criar** — auto-import analyzer via NuGet |
| 4 | `pack.sh` | **Modificar** — adicionar soft delete projects |
| 5 | `.github/workflows/publish.yml` | **Modificar** — adicionar soft delete projects |
| 6 | `AuditLog.slnx` | **Verificar** — ambos já inclusos |

---

## 1. `AuditLog.EntityFrameworkCore.SoftDelete.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>AuditLog.EntityFrameworkCore.SoftDelete</RootNamespace>
    <Description>EF Core Soft Delete integration — interceptor, ISoftDeleteEntity, query filters, cascade/restrict/setnull handlers, and source generator registry.</Description>
    <PackageId>AuditLog.EntityFrameworkCore.SoftDelete</PackageId>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" />
  </ItemGroup>
</Project>
```

Adiciona: `RootNamespace`, `Description`, `PackageId`.

---

## 2. `AuditLog.Generator.SoftDelete.csproj`

```xml
<PropertyGroup>
  ...
  <RootNamespace>AuditLog.Generator.SoftDelete</RootNamespace>
  <Description>Roslyn source generator for AuditLog SoftDelete — generates ISoftDeleteHandler<T> classes, handler registry extensions, and cascading delete logic from Fluent API configuration.</Description>
  <PackageId>AuditLog.Generator.SoftDelete</PackageId>
  <IncludeBuildOutput>true</IncludeBuildOutput>
  <DevelopmentDependency>true</DevelopmentDependency>
  <NoPackageAnalysis>true</NoPackageAnalysis>
</PropertyGroup>
```

Adiciona: `Description`, `PackageId`, `NoPackageAnalysis`.

Mantém `IncludeBuildOutput=true` + `DevelopmentDependency=true` + `NoPackageAnalysis=true` para evitar warnings NU5017/NU5128.

Atualizar o `<None>` para também incluir build props:

```xml
<ItemGroup>
  <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true" PackagePath="analyzers/dotnet/cs" Visible="false" />
  <None Include="build\AuditLog.Generator.SoftDelete.props" Pack="true" PackagePath="build\" Visible="false" />
  <None Include="build\AuditLog.Generator.SoftDelete.props" Pack="true" PackagePath="buildTransitive\" Visible="false" />
</ItemGroup>
```

---

## 3. `build/AuditLog.Generator.SoftDelete.props`

```xml
<Project>
  <ItemGroup>
    <Analyzer Include="$(MSBuildThisFileDirectory)../analyzers/dotnet/cs/AuditLog.Generator.SoftDelete.dll" Visible="false" />
  </ItemGroup>
</Project>
```

---

## 4. `pack.sh` — adicionar soft delete

```bash
CORE_PROJECTS=(
  "src/AuditLog.Abstractions/AuditLog.Abstractions.csproj"
  "src/AuditLog.EntityFrameworkCore/AuditLog.EntityFrameworkCore.csproj"
  "src/AuditLog.Generator/AuditLog.Generator.csproj"
)

SOFTDELETE_PROJECTS=(
  "src/AuditLog.EntityFrameworkCore.SoftDelete/AuditLog.EntityFrameworkCore.SoftDelete.csproj"
  "src/AuditLog.Generator.SoftDelete/AuditLog.Generator.SoftDelete.csproj"
)
```

---

## 5. `publish.yml` — adicionar soft delete

```yaml
- name: Pack core projects
  run: |
    dotnet pack src/AuditLog.Abstractions/... --output "${{ runner.temp }}/packages"
    dotnet pack src/AuditLog.EntityFrameworkCore/... --output "${{ runner.temp }}/packages"
    dotnet pack src/AuditLog.Generator/... --output "${{ runner.temp }}/packages"

    dotnet pack src/AuditLog.EntityFrameworkCore.SoftDelete/... --output "${{ runner.temp }}/packages"
    dotnet pack src/AuditLog.Generator.SoftDelete/... --output "${{ runner.temp }}/packages"
```

---

## Ordem de execução

| # | Passo |
|---|-------|
| 1 | Corrigir `ApplyConfigurationsFromAssembly` |
| 2 | Criar `build/AuditLog.Generator.SoftDelete.props` |
| 3 | Atualizar ambos `.csproj` com metadados |
| 4 | Atualizar `pack.sh` |
| 5 | Atualizar `publish.yml` |
| 6 | `dotnet pack` → validar `.nupkg` gerados |
| 7 | Commit |]
