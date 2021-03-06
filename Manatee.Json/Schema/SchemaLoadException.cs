﻿using System;

namespace Manatee.Json.Schema
{
	/// <summary>
	/// Thrown when a schema could not be loaded.
	/// </summary>
	public class SchemaLoadException : Exception
	{
		/// <summary>
		/// The results generated by meta-schema validation.
		/// </summary>
		public MetaSchemaValidationResults MetaValidation { get; }

		internal SchemaLoadException(string message, MetaSchemaValidationResults metaValidation)
			: base(message)
		{
			MetaValidation = metaValidation;
		}
	}
}