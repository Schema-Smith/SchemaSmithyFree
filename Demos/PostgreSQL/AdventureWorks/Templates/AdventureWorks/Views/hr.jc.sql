CREATE OR REPLACE VIEW "hr"."jc" AS
 SELECT jobcandidateid AS id,
    jobcandidateid,
    businessentityid,
    resume,
    modifieddate
   FROM humanresources.jobcandidate;