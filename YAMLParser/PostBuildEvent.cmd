cd "$(TargetDir)"
if not "$(ConfigurationName)"  == "Debugger" YAMLParser.exe $(ConfigurationName) "$(SolutionDir) " "$(ProjectDir).." 
echo YAMLParser.exe $(ConfigurationName) "$(SolutionDir) " "$(ProjectDir).."