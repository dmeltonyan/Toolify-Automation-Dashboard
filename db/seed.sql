USE toolify;

IF NOT EXISTS (SELECT 1 FROM dbo.employees)
BEGIN
  INSERT INTO dbo.employees(first_name,last_name,role,status,email) VALUES
  ('Ahmad','Khan','Manager','Active','ahmad@example.com'),
  ('Vishnu','Katta','Analyst','Active','vishnu@example.com'),
  ('Satya','Katta','Contractor','Contract','satya@example.com'),
  ('David','Meltonyan','Engineer','Active','david@example.com');
END
