Coding style



1. Only use var when obvious (e.g. literal, new, base.TryParse). The type must be obvious from the RHS to use var.
2. Do not use var within foreach. Use it with ordinary for (i = 0)
3. Each public class earns its own .cs file
4. Do not use magic numbers or magic strings. Unless unchanging and only present once, use a constant
5. Only omit curly braces when block can fit on one line in one statement
6. One statement per line
7. Only use abbreviations when very well-known; do not coin your own (e.g. idx, os are fine. ws for whitespace is not)

