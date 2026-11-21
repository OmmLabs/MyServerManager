import React from 'react';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';

export default function RepositoryLinks({section}) {
  const {siteConfig} = useDocusaurusContext();
  const repository = siteConfig.customFields.repositoryUrl;
  if (!repository) {
    return null;
  }

  if (section === 'download') {
    return <p><a href={`${repository}/releases`}>See tagged releases and available assets on GitHub</a>.</p>;
  }

  if (section === 'security') {
    return <p><a href={`${repository}/security`}>Open the repository Security tab</a>.</p>;
  }

  return (
    <ul>
      <li><a href={repository}>Browse the source</a></li>
      <li><a href={`${repository}/issues`}>File a reproducible issue</a></li>
      <li><a href={`${repository}/discussions`}>Join the project discussion</a></li>
    </ul>
  );
}
