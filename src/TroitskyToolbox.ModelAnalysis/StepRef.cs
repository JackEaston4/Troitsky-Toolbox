using System;
using System.Collections.Generic;
using System.Text;

namespace TroitskyToolbox.ModelAnalysis;

/// <summary>A reference to another entity instance, written <c>#123</c>.</summary>
public readonly record struct StepRef(int Id);
