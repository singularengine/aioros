import TopBar from "../../TopBar";
import IssueForm from "../IssueForm";
import { findIssue, peopleList, priorities, statuses } from "../../../lib/data";

export default function IssueView({ id }) {
  const issue = findIssue(id);
  return (
    <>
      <TopBar title="Issue" icon="issues" />
      <IssueForm issue={issue} statuses={Object.values(statuses)} priorities={priorities} people={peopleList} />
    </>
  );
}
