FROM mcr.microsoft.com/mssql/server:2019-CU27-ubuntu-20.04 

USER root

RUN apt-get update
RUN apt-get install -yq gnupg gnupg2 gnupg1 curl apt-transport-https

# Install SQL Server package links - why aren't these already embedded in the image?  How weird.
RUN curl https://packages.microsoft.com/keys/microsoft.asc -o /var/opt/mssql/ms-key.cer
RUN apt-key add /var/opt/mssql/ms-key.cer
RUN curl https://packages.microsoft.com/config/ubuntu/20.04/mssql-server-2019.list -o /etc/apt/sources.list.d/mssql-server-2019.list
RUN apt-get update

# Install SQL Server full-text-search - this only works if you add the packages references into apt-get above
RUN apt-get install -y mssql-server-fts

RUN apt-get clean
RUN rm -rf /var/lib/apt/lists

WORKDIR /tmp/devdatabase
COPY ./InitializeDatabase.sql ./
COPY ./is-ready.sh ./
COPY ./wait-for-it.sh ./
COPY ./entrypoint.sh ./
COPY ./setup.sh ./

CMD ["/bin/bash", "entrypoint.sh"]