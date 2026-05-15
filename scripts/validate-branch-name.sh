#!/bin/sh

# A simple regex linter for branch naming strategy
# based off https://www.devwithimagination.com/2020/04/13/git-commit-hooks-for-branch-naming-using-husky/

source "$ROOT_DIR/scripts/colours.sh"
echo "${YELLOW}Running validate-branch-name...${NC}"

local_branch_name="$(git rev-parse --abbrev-ref HEAD)"

validTypes="bugfix|chore|ci|docs|feature|hotfix"
valid_branch_regex="^(($validTypes)\/[a-zA-Z0-9_\-]{4,})$"

if [[ ! $local_branch_name =~ $valid_branch_regex ]]; then
    commaSeparatedList=$(echo "$validTypes" | tr '|' ', ')

    echo "${RED}Invalid branch name: $local_branch_name${NC}"
    echo "  Branch names must be in the format:"
    echo "    <type>/<subject>"
    echo "    <type>/<scope>-<subject>"
    echo "  Where:"
    echo "    - <type>: ${commaSeparatedList}"
    echo "    - <scope>: (optional) usually used for a story or issue number"
    echo "    - <subject>: at least 4 characters long"
    echo "  Examples:"
    echo "    - feature/ABC-123_subject"
    echo "    - hotfix/something_is_wrong"
    exit 1
fi

exit 0
