using Xunit;

// Testcontainer + shared HTTP host: run tests serially to avoid concurrent
// migration runs, data mutations, and connection storms against the one
// PostgreSQL container. POC-friendly; a real project might partition test
// classes into parallel collections that each own their own container.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
