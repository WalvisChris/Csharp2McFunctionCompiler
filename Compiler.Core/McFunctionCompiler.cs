using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Compiler.Core
{
    public class McFunctionCompiler : CSharpSyntaxWalker
    {
        private readonly Dictionary<string, List<string>> _compiledFiles = new Dictionary<string, List<string>>();
        private string _currentFunction = string.Empty;
        private const string ScoreboardName = "variables";

        private bool _isDiscoveryPass = true;
        private bool _hasVariables = false; // Tracks if we need to create a scoreboard

        private string _currentExecutionPrefix = "";

        // --- IF / ELSE STATEMENT LOGICA ---
        public override void VisitIfStatement(IfStatementSyntax node)
        {
            if (_isDiscoveryPass) { base.VisitIfStatement(node); return; }

            // 1. Ontleed de conditie (bijv. timer >= 100)
            if (node.Condition is BinaryExpressionSyntax binaryExpr)
            {
                string leftVar = binaryExpr.Left.ToString().Trim();
                string rightValue = binaryExpr.Right.ToString().Trim();
                string op = binaryExpr.OperatorToken.Text;

                string scoreCondition = "";

                // Vertaal C# operators naar Minecraft Scoreboard matches ranges
                if (op == "==") scoreCondition = $"score ${leftVar} {ScoreboardName} matches {rightValue}";
                else if (op == ">=") scoreCondition = $"score ${leftVar} {ScoreboardName} matches {rightValue}..";
                else if (op == "<=") scoreCondition = $"score ${leftVar} {ScoreboardName} matches ..{rightValue}";
                else if (op == ">") scoreCondition = $"score ${leftVar} {ScoreboardName} matches {int.Parse(rightValue) + 1}..";
                else if (op == "<") scoreCondition = $"score ${leftVar} {ScoreboardName} matches ..{int.Parse(rightValue) - 1}";

                if (!string.IsNullOrEmpty(scoreCondition))
                {
                    // Bewaar de oude prefix voor het geval we in geneste IF-statements zitten
                    string oldPrefix = _currentExecutionPrefix;

                    // Bouw de nieuwe prefix
                    _currentExecutionPrefix = $"execute if {scoreCondition} run ";

                    // 2. Bezoek het IF-block (alle commando's hierbinnen krijgen nu automatisch de prefix!)
                    Visit(node.Statement);

                    // 3. ELSE-block afhandelen (indien aanwezig)
                    if (node.Else != null)
                    {
                        // Voor een ELSE-statement draaien we de logica om (execute unless ...)
                        _currentExecutionPrefix = $"execute unless {scoreCondition} run ";
                        Visit(node.Else.Statement);
                    }

                    // Herstel de oude prefix
                    _currentExecutionPrefix = oldPrefix;
                }
            }
        }

        public Dictionary<string, List<string>> Compile(CSharpSyntaxNode root)
        {
            _compiledFiles.Clear();
            _hasVariables = false; // Reset for this compilation run

            // PASS 1: Read all fields/globals first to discover variables
            _isDiscoveryPass = true;
            Visit(root);

            // --- DYNAMIC SCOREBOARD INJECTION ---
            string target = "start";
            var startCommands = new List<string>();

            // Only add the creation command if the script actually defines an int!
            if (_hasVariables)
            {
                startCommands.Add($"scoreboard objectives add {ScoreboardName} dummy");
            }

            // If Pass 1 added some variable initializations to 'start', append them
            if (_compiledFiles.ContainsKey(target))
            {
                startCommands.AddRange(_compiledFiles[target]);
            }

            // Save it back to the dictionary (if we generated any setup or if it's needed)
            if (startCommands.Count > 0)
            {
                _compiledFiles[target] = startCommands;
            }

            // PASS 2: Read method bodies and generate actual function files
            _isDiscoveryPass = false;
            Visit(root);

            return _compiledFiles;
        }

        private void Emit(string command)
        {
            if (string.IsNullOrEmpty(_currentFunction)) return;

            if (!_compiledFiles.ContainsKey(_currentFunction))
            {
                _compiledFiles[_currentFunction] = new List<string>();
            }

            // NIEUW: Plak de actieve IF/ELSE prefix voor het commando!
            string finalCommand = $"{_currentExecutionPrefix}{command}";
            _compiledFiles[_currentFunction].Add(finalCommand);
        }

        private void ForceEmitToFunction(string functionName, string command)
        {
            string target = functionName.ToLower();
            if (!_compiledFiles.ContainsKey(target))
            {
                _compiledFiles[target] = new List<string>();
            }
            _compiledFiles[target].Add(command);
        }

        // --- GLOBAL FIELDS ---
        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (node.Declaration.Type.ToString() == "int")
            {
                _hasVariables = true; // Flip the flag! We found an int

                if (_isDiscoveryPass)
                {
                    foreach (var variable in node.Declaration.Variables)
                    {
                        string varName = variable.Identifier.Text;
                        var initializer = variable.Initializer;

                        if (initializer != null)
                        {
                            string varValue = initializer.Value.ToString();
                            ForceEmitToFunction("start", $"scoreboard players set ${varName} {ScoreboardName} {varValue}");
                        }
                    }
                }
            }

            if (_isDiscoveryPass)
            {
                base.VisitFieldDeclaration(node);
            }
        }

        // --- METHODS / VOIDS ---
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!_isDiscoveryPass)
            {
                string methodName = node.Identifier.Text.ToLower();
                string oldFunction = _currentFunction;
                _currentFunction = methodName;

                if (!_compiledFiles.ContainsKey(_currentFunction))
                {
                    _compiledFiles[_currentFunction] = new List<string>();
                }

                base.VisitMethodDeclaration(node);
                _currentFunction = oldFunction;
            }
            else
            {
                base.VisitMethodDeclaration(node);
            }
        }

        // --- METHOD CALLS / INVOCATIONS ---
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (_isDiscoveryPass) { base.VisitInvocationExpression(node); return; }

            string methodName = node.Expression.ToString();

            if (methodName == "Command.Print" || methodName == "Command.Say")
            {
                var argument = node.ArgumentList.Arguments.FirstOrDefault();
                if (argument != null)
                {
                    string cleanText = argument.Expression.ToString().Trim('"');
                    string cmdPrefix = methodName == "Command.Print" ? "tellraw @a " : "say ";
                    Emit($"{cmdPrefix}\"{cleanText}\"");
                }

                return;
            }
            else if (methodName == "Command.Raw")
            {
                var argument = node.ArgumentList.Arguments.FirstOrDefault();
                if (argument != null)
                {
                    // Haal de rauwe tekst op en strip de C# quotes ("...") eraf
                    string rawCommand = argument.Expression.ToString().Trim('"');

                    // Schrijf het commando letterlijk weg naar de huidige functie
                    Emit(rawCommand);
                }

                return; // CRITICAL: Stop met verder naar beneden zoeken in deze expressie
            }
            else if (methodName == "Command.Summon")
            {
                var arguments = node.ArgumentList.Arguments;

                if (arguments.Count >= 2)
                {
                    // 1. Extract Entity
                    string rawEntityText = arguments[0].Expression.ToString();
                    string minecraftEntityId = rawEntityText.Split('.').Last().ToLower();

                    // 2. Extract Position (Je bestaande float parsing code)
                    string rawPositionText = arguments[1].Expression.ToString();
                    string minecraftCoordinates = "~ ~ ~";
                    bool isRelative = rawPositionText.Contains("Position.Relative");

                    int openParen = rawPositionText.IndexOf('(');
                    int closeParen = rawPositionText.LastIndexOf(')');
                    if (openParen != -1 && closeParen != -1 && closeParen > openParen + 1)
                    {
                        string innerArgs = rawPositionText.Substring(openParen + 1, closeParen - openParen - 1);
                        string[] coordParts = innerArgs.Split(',');
                        if (coordParts.Length == 3)
                        {
                            string x = coordParts[0].Replace("f", "").Trim();
                            string y = coordParts[1].Replace("f", "").Trim();
                            string z = coordParts[2].Replace("f", "").Trim();
                            string prefix = isRelative ? "~" : "";
                            string xStr = (isRelative && x == "0") ? "~" : prefix + x;
                            string yStr = (isRelative && y == "0") ? "~" : prefix + y;
                            string zStr = (isRelative && z == "0") ? "~" : prefix + z;
                            minecraftCoordinates = $"{xStr} {yStr} {zStr}";
                        }
                    }

                    // 3. Extract OPTIONELE NBT Data
                    string nbtOutput = "";
                    if (arguments.Count >= 3)
                    {
                        var nbtExpression = arguments[2].Expression;
                        string rawNbtText = nbtExpression.ToString();

                        if (nbtExpression is LiteralExpressionSyntax)
                        {
                            // Scenario A: Gewone string: "{NoAI:1b}"
                            nbtOutput = " " + rawNbtText.Trim('"');
                        }
                        else if (nbtExpression is AnonymousObjectCreationExpressionSyntax anonObject)
                        {
                            // Scenario B: GEBRUIKT NU CORRECT 'NameEquals' in plaats van 'NameEquivalance'
                            var nbtPairs = anonObject.Initializers.Select(prop =>
                            {
                                string key = prop.NameEquals != null ? prop.NameEquals.Name.ToString().Trim() : "";
                                string val = prop.Expression.ToString().Replace("f", "").Trim('"');
                                return $"{key}:{val}";
                            }).Where(pair => !pair.StartsWith(":"));

                            nbtOutput = " {" + string.Join(",", nbtPairs) + "}";
                        }
                        else if (nbtExpression is ObjectCreationExpressionSyntax objectCreation)
                        {
                            // Scenario C: Type-safe NBT stijl (new Nbt { ... })
                            var nbtPairs = new List<string>();

                            if (objectCreation.Initializer != null)
                            {
                                foreach (var expression in objectCreation.Initializer.Expressions)
                                {
                                    if (expression is AssignmentExpressionSyntax assignment)
                                    {
                                        string key = assignment.Left.ToString().Trim();
                                        var rightSide = assignment.Right;

                                        // 1. CHECK: Is de rechterkant een C# Collection Expression (bijv. Tags = ["test"])?
                                        if (rightSide is Microsoft.CodeAnalysis.CSharp.Syntax.CollectionExpressionSyntax collectionExpr)
                                        {
                                            var elements = collectionExpr.Elements.Select(el => {
                                                string cleanEl = el.ToString().Trim('"');
                                                return $"\"{cleanEl}\"";
                                            });
                                            string arrayString = $"[{string.Join(",", elements)}]";
                                            nbtPairs.Add($"{key}:{arrayString}");
                                        }
                                        // 2. CHECK: Is het een boolean literal (true of false)?
                                        else if (rightSide is Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax literal &&
                                                 (literal.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.TrueLiteralExpression ||
                                                  literal.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.FalseLiteralExpression))
                                        {
                                            // Vertaal true naar 1b en false naar 0b
                                            string boolValue = literal.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.TrueLiteralExpression ? "1b" : "0b";
                                            nbtPairs.Add($"{key}:{boolValue}");
                                        }
                                        // 3. FALLBACK: Gewone string of getal waarde
                                        else
                                        {
                                            string val = rightSide.ToString().Replace("f", "").Trim('"');
                                            nbtPairs.Add($"{key}:{val}");
                                        }
                                    }
                                }
                            }
                            nbtOutput = " {" + string.Join(",", nbtPairs) + "}";
                        }
                    }

                    // Voeg de hele command samen incl. de optionele NBT
                    Emit($"summon {minecraftEntityId} {minecraftCoordinates}{nbtOutput}");
                }

                return;
            }
            else if (methodName == "Command.ScoreboardDisplay")
            {
                var argument = node.ArgumentList.Arguments.FirstOrDefault();
                if (argument != null)
                {
                    string rawSlotText = argument.Expression.ToString();
                    string minecraftSlot = rawSlotText.Split('.').Last().ToLower();
                    Emit($"scoreboard objectives setdisplay {minecraftSlot} {ScoreboardName}");
                }

                return;
            }
            else
            {
                string mcFunctionName = methodName.ToLower();
                Emit($"function compiler:{mcFunctionName}");
            }

            base.VisitInvocationExpression(node);
        }

        // --- LOCAL VARIABLES ---
        public override void VisitVariableDeclaration(VariableDeclarationSyntax node)
        {
            if (node.Type.ToString() == "int")
            {
                _hasVariables = true; // Flip the flag for local ints too!

                if (!_isDiscoveryPass)
                {
                    foreach (var variable in node.Variables)
                    {
                        string varName = variable.Identifier.Text;
                        var initializer = variable.Initializer;
                        if (initializer != null)
                        {
                            string varValue = initializer.Value.ToString();
                            Emit($"scoreboard players set ${varName} {ScoreboardName} {varValue}");
                        }
                    }
                }
            }
            base.VisitVariableDeclaration(node);
        }

        // --- MATH AND UPDATES (+=, -=, =) ---
        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (_isDiscoveryPass) { base.VisitAssignmentExpression(node); return; }

            string varName = node.Left.ToString();
            string newValue = node.Right.ToString();
            string operatorToken = node.OperatorToken.Text;

            if (operatorToken == "=")
            {
                Emit($"scoreboard players set ${varName} {ScoreboardName} {newValue}");
            }
            else if (operatorToken == "+=")
            {
                Emit($"scoreboard players add ${varName} {ScoreboardName} {newValue}");
            }
            else if (operatorToken == "-=")
            {
                Emit($"scoreboard players remove ${varName} {ScoreboardName} {newValue}");
            }

            base.VisitAssignmentExpression(node);
        }

        public Dictionary<string, List<string>> GetCompiledFiles() => _compiledFiles;
    }
}