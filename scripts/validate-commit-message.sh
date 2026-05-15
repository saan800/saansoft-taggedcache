#!/bin/sh

# A simple regex commit linter example
# https://www.conventionalcommits.org/en/v1.0.0/
# https://github.com/angular/angular/blob/22b96b9/CONTRIBUTING.md#type

source "$ROOT_DIR/scripts/colours.sh"
echo "${YELLOW}Running validate-commit-message...${NC}"

# Check if the correct number of arguments are provided
if [ $# -ne 1 ]; then
    echo "${RED}Usage: $0 <commit_message>${NC}"
    exit 1
fi

# Read the commit message from the arguments
commit_message="$1"
validTypes="BREAKING CHANGE|build|chore|ci|docs|feat|fix|perf|refactor|revert|style|test"
system_msg_regex="^(Merge remote-tracking branch|Revert) .*"
valid_msg_regex="^($validTypes)(\(.{3,}\))?!?: .{4,}"

if [[ ! $commit_message =~ $valid_msg_regex ]] && [[ ! $commit_message =~ $system_msg_regex ]]; then
    commaSeparatedList=$(echo "$validTypes" | tr '|' ', ')

    echo "${RED}Invalid commit message: $commit_message${NC}"
    echo "  Commit messages must be in the Conventional Commits format:"
    echo "    <type>: <subject>"
    echo "    <type>(<scope>): <subject>"
    echo "  Where:"
    echo "    - <type>: $commaSeparatedList"
    echo "    - <scope>: (optional) usually used for story or issue number"
    echo "    - <subject>: at least 4 characters long"
    echo "  Examples:"
    echo "    - feature(ABC-123): subject"
    echo "    - fix: subject"
    echo "  More info: https://www.conventionalcommits.org/en/v1.0.0/"
    echo ""
    echo "  ${GREEN}HELP:${NC}"
    echo "    - If you are using zhs command line, and the '!' character for breaking changes. You may get the error:"
    echo "      > zsh: illegal modifier:"
    echo "      In your commit message use single quotes around the commit message."


    exit 1
fi

exit 0
