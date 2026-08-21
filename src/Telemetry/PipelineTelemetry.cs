using System.Diagnostics;

namespace PoliNorError.Extensions.Http
{
	/// <summary>
	/// Provides the <see cref="ActivitySource"/> for the PoliNorError.Extensions.Http library.
	/// Subscribe to the <see cref="SourceName"/> via OpenTelemetry or <see cref="ActivityListener"/>
	/// to observe resilience pipeline execution.
	/// </summary>
	/// <remarks>
	/// Activity tags emitted by this library:
	/// <list type="bullet">
  ///   <item><c>polinorerror.pipeline.result</c>: <c>"success"</c>, <c>"failed"</c>, or <c>"canceled"</c></item>
  ///   <item><c>polinorerror.pipeline.is_final_handler</c>: <c>true</c> if this handler is the final (response-classifying) handler</item>
  ///   <item><c>polinorerror.pipeline.policy.type</c>: short name of the PoliNorError policy type (e.g. <c>"RetryPolicy"</c>, <c>"FallbackPolicy"</c>)</item>
	/// </list>
	/// </remarks>
	public static class PipelineTelemetry
	{
		/// <summary>
		/// The well-known name used to identify the <see cref="ActivitySource"/>.
		/// Pass this to <c>TracerProviderBuilder.AddSource()</c> or <c>ActivitySource.AddActivityListener()</c>.
		/// </summary>
		public const string SourceName = "PoliNorError.Extensions.Http";

		/// <summary>
		/// The <see cref="ActivitySource"/> used to create activities for pipeline execution.
		/// This is safe to use statically — when no listener is attached, all operations are no-alloc no-ops.
		/// </summary>
		public static readonly ActivitySource Source = new ActivitySource(SourceName);

		/// <summary>
		/// The operation name for the top-level pipeline Activity.
		/// </summary>
		internal const string PipelineOperationName = "HttpPipeline.Execute";
	}
}
