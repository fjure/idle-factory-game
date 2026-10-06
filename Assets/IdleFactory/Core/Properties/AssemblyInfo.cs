using System.Runtime.CompilerServices;

// The EditMode test assembly exercises the internal V1 import (T141, F6) without the import being public API.
[assembly: InternalsVisibleTo("IdleFactory.EditModeTests")]
