// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1835:Prefer the 'Memory'-based overloads for 'ReadAsync' and 'WriteAsync'", Justification = "Should be rewritten", Scope = "member", Target = "~M:CloudNative.CloudEvents.Core.BinaryDataUtilities.CopyToStreamAsync(System.ReadOnlyMemory{System.Byte},System.IO.Stream)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Performance", "CA1866:Use char overload", Justification = "Not supported for netstandard2.0", Scope = "member", Target = "~M:CloudNative.CloudEvents.CloudEventAttributeType.UriType.ParseImpl(System.String)~System.Uri")]
