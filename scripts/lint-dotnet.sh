#!/bin/sh

source "$ROOT_DIR/scripts/colours.sh"
echo "${YELLOW}Running lint-dotnet...${NC}"

# Check if the correct number of arguments are provided
if [ $# -lt 1 ]; then
    echo "${RED}Usage: $0 <list_of_files> [options]${NC}"
    echo "  - options: see ${PURPLE}dotnet format --help${NC} for details"
    echo "             eg ${PURPLE}--verify-no-changes${NC} for verification in CI pipeline"
    exit 1
fi

# Assign arguments to variables
files="$1"
staged_cs_files=$(echo "$files" | grep -E ".cs$")
# Get additional arguments if provided
if [ $# -gt 1 ]; then
    shift  # Shift the arguments to remove the first one
    additional_args="$@"
    echo "Additional arguments: $additional_args"
fi

if [ -n "$staged_cs_files" ]; then
    dotnet format --verbosity normal --no-restore --include $staged_cs_files $additional_args
fi
