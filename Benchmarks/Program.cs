using BenchmarkDotNet.Running;

using Requirement;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);