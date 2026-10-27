using System.Runtime.CompilerServices;

// The EditMode test assembly drives the internal content-load seams (T141): the catalog-factory overload of
// RunCatalogBuilder.Build and the GameplayContentLoadResult factories.
[assembly: InternalsVisibleTo("IdleFactory.EditModeTests")]
