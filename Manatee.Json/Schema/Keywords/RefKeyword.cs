﻿using System;
using System.Diagnostics;
using System.Linq;
using Manatee.Json.Internal;
using Manatee.Json.Pointer;
using Manatee.Json.Serialization;

namespace Manatee.Json.Schema
{
	/// <summary>
	/// Defines the `$ref` JSON Schema keyword.
	/// </summary>
	[DebuggerDisplay("Name={Name} Value={Reference}")]
	public class RefKeyword : IJsonSchemaKeyword, IEquatable<RefKeyword>
	{
		private JsonSchema _resolvedRoot;
		private JsonPointer _resolvedFragment;

		/// <summary>
		/// Gets the name of the keyword.
		/// </summary>
		public string Name => "$ref";
		/// <summary>
		/// Gets the versions (drafts) of JSON Schema which support this keyword.
		/// </summary>
		public JsonSchemaVersion SupportedVersions { get; } = JsonSchemaVersion.All;
		/// <summary>
		/// Gets the a value indicating the sequence in which this keyword will be evaluated.
		/// </summary>
		public int ValidationSequence => 0;
		/// <summary>
		/// Gets the vocabulary that defines this keyword.
		/// </summary>
		public SchemaVocabulary Vocabulary => SchemaVocabularies.Core;

		/// <summary>
		/// Gets the reference value for this keyword.
		/// </summary>
		public string Reference { get; private set; }

		/// <summary>
		/// Gets the resolved schema that corresponds to the reference.
		/// </summary>
		public JsonSchema Resolved { get; private set; }

		/// <summary>
		/// Used for deserialization.
		/// </summary>
		[DeserializationUseOnly]
		public RefKeyword() { }
		/// <summary>
		/// Creates an instance of the <see cref="RefKeyword"/>.
		/// </summary>
		public RefKeyword(string reference)
		{
			Reference = reference;
		}

		/// <summary>
		/// Provides the validation logic for this keyword.
		/// </summary>
		/// <param name="context">The context object.</param>
		/// <returns>Results object containing a final result and any errors that may have been found.</returns>
		public SchemaValidationResults Validate(SchemaValidationContext context)
		{
			if (Resolved == null)
			{
				_ResolveReference(context);
				if (Resolved == null)
					throw new SchemaReferenceNotFoundException(context.RelativeLocation);
			}

			var results = new SchemaValidationResults(Name, context);

			var newContext = new SchemaValidationContext(context)
				{
					BaseUri = _resolvedRoot?.DocumentPath,
					Instance = context.Instance,
					Root = _resolvedRoot ?? context.Root,
					BaseRelativeLocation = _resolvedFragment?.WithHash(),
					RelativeLocation = context.RelativeLocation.CloneAndAppend(Name),
				};
			var nestedResults = Resolved.Validate(newContext);

			results.IsValid = nestedResults.IsValid;
			results.NestedResults.Add(nestedResults);

			return results;
		}
		/// <summary>
		/// Used register any subschemas during validation.  Enables look-forward compatibility with `$ref` keywords.
		/// </summary>
		/// <param name="baseUri">The current base URI</param>
		/// <param name="localRegistry"></param>
		public void RegisterSubschemas(Uri baseUri, JsonSchemaRegistry localRegistry) { }
		/// <summary>
		/// Resolves any subschemas during resolution of a `$ref` during validation.
		/// </summary>
		/// <param name="pointer">A <see cref="JsonPointer"/> to the target schema.</param>
		/// <param name="baseUri">The current base URI.</param>
		/// <returns>The referenced schema, if it exists; otherwise null.</returns>
		public JsonSchema ResolveSubschema(JsonPointer pointer, Uri baseUri)
		{
			return null;
		}
		/// <summary>
		/// Builds an object from a <see cref="JsonValue"/>.
		/// </summary>
		/// <param name="json">The <see cref="JsonValue"/> representation of the object.</param>
		/// <param name="serializer">The <see cref="JsonSerializer"/> instance to use for additional
		/// serialization of values.</param>
		public void FromJson(JsonValue json, JsonSerializer serializer)
		{
			Reference = json.String;
		}

		/// <summary>
		/// Converts an object to a <see cref="JsonValue"/>.
		/// </summary>
		/// <param name="serializer">The <see cref="JsonSerializer"/> instance to use for additional
		/// serialization of values.</param>
		/// <returns>The <see cref="JsonValue"/> representation of the object.</returns>
		public JsonValue ToJson(JsonSerializer serializer)
		{
			return Reference;
		}
		/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
		/// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
		/// <param name="other">An object to compare with this object.</param>
		public bool Equals(RefKeyword other)
		{
			if (other is null) return false;
			if (ReferenceEquals(this, other)) return true;
			return string.Equals(Reference, other.Reference);
		}
		/// <summary>Indicates whether the current object is equal to another object of the same type.</summary>
		/// <returns>true if the current object is equal to the <paramref name="other" /> parameter; otherwise, false.</returns>
		/// <param name="other">An object to compare with this object.</param>
		public bool Equals(IJsonSchemaKeyword other)
		{
			return Equals(other as RefKeyword);
		}
		/// <summary>Determines whether the specified object is equal to the current object.</summary>
		/// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
		/// <param name="obj">The object to compare with the current object. </param>
		public override bool Equals(object obj)
		{
			return Equals(obj as RefKeyword);
		}
		/// <summary>Serves as the default hash function. </summary>
		/// <returns>A hash code for the current object.</returns>
		public override int GetHashCode()
		{
			return Reference?.GetHashCode() ?? 0;
		}

		private void _ResolveReference(SchemaValidationContext context)
		{
			if (Reference.IsLocalSchemaId())
			{
				Resolved = context.LocalRegistry.GetLocal(Reference);
				if (Resolved != null) return;
			}

			var documentPath = context.BaseUri;
			var referenceParts = Reference.Split(new[] { '#' }, StringSplitOptions.None);
			var address = string.IsNullOrWhiteSpace(referenceParts[0]) ? documentPath?.OriginalString : referenceParts[0];
			_resolvedFragment = referenceParts.Length > 1 ? JsonPointer.Parse(referenceParts[1]) : new JsonPointer();
			if (!string.IsNullOrWhiteSpace(address))
			{
				if (!Uri.TryCreate(address, UriKind.Absolute, out var absolute) &&
					(JsonSchemaOptions.RefResolution == RefResolutionStrategy.ProcessSiblingId ||
					 context.Root.SupportedVersions == JsonSchemaVersion.Draft2019_09))
					address = context.Local.Id + address;

				if (documentPath != null && !Uri.TryCreate(address, UriKind.Absolute, out absolute))
				{
					var uriFolder = documentPath.OriginalString.EndsWith("/") ? documentPath : documentPath.GetParentUri();
					absolute = new Uri(uriFolder, address);
					address = absolute.OriginalString;
				}

				_resolvedRoot = JsonSchemaRegistry.Get(address);
			}
			else
				_resolvedRoot = context.Root;

			if (_resolvedRoot == null) return;

			var wellKnown = JsonSchemaRegistry.GetWellKnown(Reference);
			if (wellKnown != null)
			{
				Resolved = wellKnown;
				return;
			}

			_ResolveLocalReference(_resolvedRoot?.DocumentPath ?? context.BaseUri);
		}
		private void _ResolveLocalReference(Uri baseUri)
		{
			if (!_resolvedFragment.Any())
			{
				Resolved = _resolvedRoot;
				return;
			}

			Resolved = _resolvedRoot.ResolveSubschema(_resolvedFragment, baseUri);
		}
	}
}